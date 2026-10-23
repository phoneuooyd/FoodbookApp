import { serve } from "std/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.45.0";

interface VerifyRequest {
  productId: string;
  purchaseToken: string;
  packageName: string;
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
  cancelSurveyResult?: string;
}

interface VerifyResponse {
  isValid: boolean;
  plan: "free" | "premium_monthly" | "premium_yearly" | null;
  premiumFrom: string | null;
  premiumTo: string | null;
  error?: string;
}

const PRODUCT_PLAN_MAP: Record<string, "premium_monthly" | "premium_yearly"> = {
  foodbook_premium_monthly: "premium_monthly",
  foodbook_premium_yearly: "premium_yearly",
};

const SERVICE_ACCOUNT_JSON = Deno.env.get("GOOGLE_SERVICE_ACCOUNT_JSON");
const SUPABASE_URL = Deno.env.get("SUPABASE_URL") ?? "";
const SERVICE_ROLE_KEY = Deno.env.get("SERVICE_ROLE_KEY") ?? "";
const EXPECTED_PACKAGE_NAME = "com.ppapps.foodbookapp";

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

async function verifyWithGoogleApi(
  accessToken: string,
  packageName: string,
  productId: string,
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
    const body = await res.text();
    console.warn(`[verify-purchase] Acknowledge warning ${res.status}: ${body}`);
  }
}

function planToUserType(plan: string | null): number {
  return plan === "premium_monthly" || plan === "premium_yearly" ? 1 : 0;
}

serve(async (req) => {
  const authHeader = req.headers.get("Authorization") ?? "";
  if (!authHeader.startsWith("Bearer ")) {
    return new Response(JSON.stringify({ isValid: false, error: "Missing or invalid Authorization header" }), {
      status: 401,
      headers: { "Content-Type": "application/json" },
    });
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE_KEY);
  const token = authHeader.slice(7);
  const { data: { user }, error: authError } = await supabase.auth.getUser(token);
  if (authError || !user) {
    return new Response(JSON.stringify({ isValid: false, error: "Invalid or expired token" }), {
      status: 401,
      headers: { "Content-Type": "application/json" },
    });
  }

  let body: VerifyRequest;
  try {
    body = await req.json();
  } catch {
    return new Response(JSON.stringify({ isValid: false, error: "Invalid JSON body" }), {
      status: 400,
      headers: { "Content-Type": "application/json" },
    });
  }

  const { productId, purchaseToken, packageName } = body;

  if (!productId || !purchaseToken || !packageName) {
    return new Response(JSON.stringify({ isValid: false, error: "Missing required fields: productId, purchaseToken, packageName" }), {
      status: 400,
      headers: { "Content-Type": "application/json" },
    });
  }

  if (packageName !== EXPECTED_PACKAGE_NAME) {
    return new Response(JSON.stringify({ isValid: false, error: "Invalid package name" }), {
      status: 400,
      headers: { "Content-Type": "application/json" },
    });
  }

  const plan = PRODUCT_PLAN_MAP[productId];
  if (!plan) {
    return new Response(JSON.stringify({ isValid: false, error: `Unknown product ID: ${productId}` }), {
      status: 400,
      headers: { "Content-Type": "application/json" },
    });
  }

  if (!SERVICE_ACCOUNT_JSON) {
    return new Response(JSON.stringify({ isValid: false, error: "Server configuration error: missing service account" }), {
      status: 500,
      headers: { "Content-Type": "application/json" },
    });
  }

  try {
    const accessToken = await getGoogleAccessToken();
    const sub = await verifyWithGoogleApi(accessToken, packageName, productId, purchaseToken);

    const now = new Date().toISOString();
    const startTime = sub.startTime ?? now;
    const expiryTime = sub.expiryTime ?? now;

    if (sub.acknowledgementState === 0) {
      await acknowledgePurchase(accessToken, packageName, productId, purchaseToken);
    }

    const purchaseRecord = {
      user_id: user.id,
      purchase_token: purchaseToken,
      product_id: productId,
      package_name: packageName,
      order_id: sub.latestOrderId ?? null,
      subscription_state: sub.subscriptionState,
      start_time: startTime,
      expiry_time: expiryTime,
      auto_renewing: sub.autoRenewing ?? false,
      acknowledgement_state: sub.acknowledgementState ?? 0,
      last_verified_at: now,
    };

    const { error: upsertError } = await supabase
      .from("subscription_purchases")
      .upsert(purchaseRecord, { onConflict: "purchase_token" });

    if (upsertError) {
      console.error(`[verify-purchase] Upsert failed: ${upsertError.message}`);
    }

    const { error: eventError } = await supabase
      .from("subscription_events")
      .insert({
        event_idempotency_key: `${purchaseToken}_purchase_verified`,
        user_id: user.id,
        event_type: "purchase_verified",
        purchase_token: purchaseToken,
        product_id: productId,
        metadata: { plan, subscriptionState: sub.subscriptionState, autoRenewing: sub.autoRenewing },
        created_at: now,
      });

    if (eventError) {
      console.warn(`[verify-purchase] Event insert skipped (idempotent): ${eventError.message}`);
    }

    const isPremium = sub.subscriptionState === 1 && sub.expiryTime > now;

    const { error: userUpdateError } = await supabase
      .from("users")
      .update({
        is_premium: isPremium,
        premium_from: startTime,
        premium_to: expiryTime,
        user_type: planToUserType(plan),
        updated_at: now,
      })
      .eq("id", user.id);

    if (userUpdateError) {
      console.error(`[verify-purchase] User update failed: ${userUpdateError.message}`);
    }

    const response: VerifyResponse = {
      isValid: true,
      plan,
      premiumFrom: startTime,
      premiumTo: expiryTime,
    };

    return new Response(JSON.stringify(response), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`[verify-purchase] Error: ${message}`, err);

    const now = new Date().toISOString();
    await supabase.from("subscription_events").insert({
      event_idempotency_key: `${purchaseToken}_purchase_verification_failed`,
      user_id: user.id,
      event_type: "purchase_verification_failed",
      purchase_token: purchaseToken,
      product_id: productId,
      metadata: { error: message },
      created_at: now,
    }).catch(() => {});

    return new Response(JSON.stringify({
      isValid: false,
      plan: null,
      premiumFrom: null,
      premiumTo: null,
      error: "Verification failed. Please try again or contact support.",
    } as VerifyResponse), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  }
});
