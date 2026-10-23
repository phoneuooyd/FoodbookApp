import { serve } from "std/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.45.0";

interface PubSubEnvelope {
  message: {
    data: string;
    messageId: string;
    publishTime: string;
    attributes?: Record<string, string>;
  };
  subscription: string;
}

interface DeveloperNotification {
  version: string;
  packageName: string;
  eventTimeMillis: string;
  subscriptionNotification?: {
    version: string;
    notificationType: number;
    purchaseToken: string;
    subscriptionId: string;
  };
  testNotification?: {
    version: string;
  };
}

interface GoogleSubscriptionPurchase {
  kind: string;
  startTime: string;
  expiryTime: string;
  latestOrderId: string;
  autoRenewing: boolean;
  cancelReason?: number;
  linkedPurchaseToken?: string;
  purchaseType?: number;
  acknowledgementState?: number;
  subscriptionState: number;
  userCancellationTime?: string;
}

const RTDN_SECRET = Deno.env.get("FOODBOOK_RTDN_SECRET") ?? "";
const SERVICE_ACCOUNT_JSON = Deno.env.get("GOOGLE_SERVICE_ACCOUNT_JSON");
const SUPABASE_URL = Deno.env.get("SUPABASE_URL") ?? "";
const SERVICE_ROLE_KEY = Deno.env.get("SERVICE_ROLE_KEY") ?? "";
const EXPECTED_PACKAGE_NAME = "com.ppapps.foodbookapp";

const EVENT_TYPE_MAP: Record<number, string> = {
  1: "subscription_recovered",
  2: "subscription_renewed",
  3: "subscription_canceled",
  4: "subscription_purchased",
  5: "subscription_on_hold",
  6: "subscription_in_grace_period",
  7: "subscription_restarted",
  8: "subscription_price_change_confirmed",
  9: "subscription_deferred",
  10: "subscription_paused",
  11: "subscription_pause_schedule_changed",
  12: "subscription_expired",
  13: "subscription_revoked",
};

function getGoogleAccessToken(): Promise<string> {
  const serviceAccount = JSON.parse(SERVICE_ACCOUNT_JSON!);
  const { client_email, private_key } = serviceAccount;
  const jwtHeader = { alg: "RS256", typ: "JWT" };
  const now = Math.floor(Date.now() / 1000);
  const jwtClaim = {
    iss: client_email,
    scope: "https://www.googleapis.com/auth/androidpublisher",
    aud: "https://oauth2.googleapis.com/token",
    exp: now + 3600,
    iat: now,
  };

  function base64UrlEncode(data: string): string {
    return btoa(data).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  }

  function strToB64(s: string): string {
    return base64UrlEncode(new TextEncoder().encode(s).reduce((acc, b) => acc + String.fromCharCode(b), ""));
  }

  function signRsaSha256(data: string, key: string): Promise<string> {
    const encoder = new TextEncoder();
    const pkcs8 = key
      .replace("-----BEGIN PRIVATE KEY-----", "")
      .replace("-----END PRIVATE KEY-----", "")
      .replace(/\n/g, "");
    const binaryKey = Uint8Array.from(atob(pkcs8), (c) => c.charCodeAt(0));

    return crypto.subtle.importKey("pkcs8", binaryKey, { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" }, false, ["sign"])
      .then((key) => crypto.subtle.sign("RSASSA-PKCS1-v1_5", key, encoder.encode(data)))
      .then((sig) => base64UrlEncode(String.fromCharCode(...new Uint8Array(sig))));
  }

  const message = `${strToB64(JSON.stringify(jwtHeader))}.${strToB64(JSON.stringify(jwtClaim))}`;
  return signRsaSha256(message, private_key!).then((signature) => {
    const assertion = `${message}.${signature}`;
    return fetch("https://oauth2.googleapis.com/token", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
        assertion,
      }),
    }).then((r) => r.json()).then((data: { access_token: string }) => data.access_token);
  });
}

async function fetchSubscriptionState(
  accessToken: string,
  packageName: string,
  purchaseToken: string,
): Promise<GoogleSubscriptionPurchase> {
  const url = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${packageName}/purchases/subscriptionsv2/tokens/${purchaseToken}`;
  const res = await fetch(url, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`Google API error ${res.status}: ${body}`);
  }
  return res.json();
}

async function acknowledgePurchase(
  accessToken: string,
  packageName: string,
  productId: string,
  purchaseToken: string,
): Promise<void> {
  const url = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${packageName}/purchases/subscriptionsv2/tokens/${purchaseToken}:acknowledge`;
  const res = await fetch(url, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({}),
  });
  if (!res.ok && res.status !== 409) {
    console.warn(`[handle-rtdn] Acknowledge warning ${res.status}`);
  }
}

function planToUserType(productId: string): number {
  return productId === "foodbook_premium_monthly" || productId === "foodbook_premium_yearly" ? 1 : 0;
}

serve(async (req) => {
  const url = new URL(req.url);
  const secret = url.searchParams.get("secret");

  if (!secret || secret !== RTDN_SECRET) {
    return new Response("Unauthorized", { status: 401 });
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE_KEY);

  let envelope: PubSubEnvelope;
  try {
    envelope = await req.json();
  } catch {
    return new Response("Invalid Pub/Sub envelope", { status: 400 });
  }

  if (!envelope?.message?.data) {
    return new Response("OK", { status: 200 });
  }

  let notification: DeveloperNotification;
  try {
    const decoded = atob(envelope.message.data);
    notification = JSON.parse(decoded);
  } catch {
    console.error("[handle-rtdn] Failed to decode notification data");
    return new Response("OK", { status: 200 });
  }

  if (notification.testNotification) {
    console.log("[handle-rtdn] Test notification received - OK");
    return new Response("OK", { status: 200 });
  }

  const subNotif = notification.subscriptionNotification;
  if (!subNotif) {
    return new Response("OK", { status: 200 });
  }

  const { purchaseToken, subscriptionId, notificationType, version: notifVersion } = subNotif;
  const eventId = `${envelope.message.messageId}_${purchaseToken}`;

  if (!SERVICE_ACCOUNT_JSON) {
    console.error("[handle-rtdn] Missing GOOGLE_SERVICE_ACCOUNT_JSON");
    return new Response("Server configuration error", { status: 500 });
  }

  try {
    const { count, error: dupCheckError } = await supabase
      .from("subscription_events")
      .select("id", { count: "exact", head: true })
      .eq("event_idempotency_key", eventId);

    if (dupCheckError) {
      console.warn(`[handle-rtdn] Dup check failed: ${dupCheckError.message}`);
    }

    if (count && count > 0) {
      console.log(`[handle-rtdn] Duplicate event skipped: ${eventId}`);
      return new Response("OK", { status: 200 });
    }
  } catch {
  }

  try {
    const accessToken = await getGoogleAccessToken();
    const sub = await fetchSubscriptionState(accessToken, EXPECTED_PACKAGE_NAME, purchaseToken);

    const now = new Date().toISOString();

    if (sub.acknowledgementState === 0) {
      await acknowledgePurchase(accessToken, EXPECTED_PACKAGE_NAME, subscriptionId, purchaseToken).catch(() => {});
    }

    const { error: upsertError } = await supabase
      .from("subscription_purchases")
      .upsert({
        purchase_token: purchaseToken,
        product_id: subscriptionId,
        package_name: EXPECTED_PACKAGE_NAME,
        order_id: sub.latestOrderId ?? null,
        subscription_state: sub.subscriptionState,
        start_time: sub.startTime ?? now,
        expiry_time: sub.expiryTime ?? now,
        auto_renewing: sub.autoRenewing ?? false,
        acknowledgement_state: sub.acknowledgementState ?? 0,
        last_verified_at: now,
      }, { onConflict: "purchase_token" });

    if (upsertError) {
      console.error(`[handle-rtdn] Upsert subscription_purchases failed: ${upsertError.message}`);
    }

    const eventType = EVENT_TYPE_MAP[notificationType] ?? `unknown_${notificationType}`;

    await supabase
      .from("subscription_events")
      .insert({
        event_idempotency_key: eventId,
        event_type: eventType,
        purchase_token: purchaseToken,
        product_id: subscriptionId,
        metadata: {
          notificationType,
          notificationVersion: notifVersion,
          subscriptionState: sub.subscriptionState,
          autoRenewing: sub.autoRenewing,
          cancelReason: sub.cancelReason,
        },
        created_at: now,
      })
      .catch((e) => console.warn(`[handle-rtdn] Event insert skipped (idempotent): ${e.message}`));

    const { data: purchaseRecord, error: purchaseQueryError } = await supabase
      .from("subscription_purchases")
      .select("user_id")
      .eq("purchase_token", purchaseToken)
      .maybeSingle();

    if (purchaseQueryError) {
      console.error(`[handle-rtdn] Failed to find purchase record: ${purchaseQueryError.message}`);
      return new Response("OK", { status: 200 });
    }

    if (!purchaseRecord?.user_id) {
      console.warn(`[handle-rtdn] No user_id found for purchase token ${purchaseToken}. Searching by product_id...`);

      const { data: purchases } = await supabase
        .from("subscription_purchases")
        .select("user_id")
        .eq("product_id", subscriptionId)
        .order("last_verified_at", { ascending: false })
        .limit(1)
        .maybeSingle();

      if (!purchases?.user_id) {
        console.warn(`[handle-rtdn] No user found for subscription ${subscriptionId}`);
        return new Response("OK", { status: 200 });
      }

      const userId = purchases.user_id;

      await supabase
        .from("subscription_purchases")
        .update({ user_id: userId })
        .eq("purchase_token", purchaseToken)
        .catch(() => {});

      const isPremium = sub.subscriptionState === 1 && sub.expiryTime > now;

      await supabase
        .from("users")
        .update({
          is_premium: isPremium,
          premium_to: sub.expiryTime ?? null,
          premium_from: sub.startTime ?? null,
          user_type: planToUserType(subscriptionId),
          updated_at: now,
        })
        .eq("id", userId);

      return new Response("OK", { status: 200 });
    }

    const userId = purchaseRecord.user_id;

    await supabase
      .from("subscription_purchases")
      .update({ user_id: userId })
      .eq("purchase_token", purchaseToken)
      .catch(() => {});
    const isPremium = sub.subscriptionState === 1 && sub.expiryTime > now;

    await supabase
      .from("users")
      .update({
        is_premium: isPremium,
        premium_to: sub.expiryTime ?? null,
        premium_from: sub.startTime ?? null,
        user_type: planToUserType(subscriptionId),
        updated_at: now,
      })
      .eq("id", userId);

    return new Response("OK", { status: 200 });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`[handle-rtdn] Error processing notification: ${message}`, err);

    return new Response("OK", { status: 200 });
  }
});
