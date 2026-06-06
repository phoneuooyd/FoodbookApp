Introduction to Open Food Facts API documentation#

Everything you need to know about Open Food Facts API.

    [!CAUTION] Are you going to use our API? Please read this documentation entirely before using it.

Overview#

Open Food Facts is a food products database made by everyone, for everyone, that can help you make better choices about what you eat. Being open data, anyone can reuse it for any purpose.

The Open Food Facts API enables developers to get information like ingredients and nutritional values of products, and even add more facts to the products database. You can use the API to build applications that allow users to contribute to the database and make healthier food choices.
API version 	Status 	Notes
v3 (latest: v3.6) 	✅ Current — recommended for all new integrations 	v3.6 introduces a new tags schema (tags_sources); see the change log
v2 	⚠️ Deprecated — still supported for backward compatibility 	Migrate to v3 for new integrations
v1 / v0 	Legacy — not recommended 	

    Data in the Open Food Facts database is provided voluntarily by users who want to support the program. As a result, there are no assurances that the data is accurate, complete, or reliable. The user assumes the entire risk of using the data.

Before You Start#

    The Open Food Facts database is available under the Open Database License.
    The individual contents of the database are available under the Database Contents License.
    Product images are available under the Creative Commons Attribution ShareAlike license. They may contain graphical elements subject to copyright or other rights that may, in some cases, be reproduced (quotation rights or fair use).

Before using the API, please :

    Read the Terms and conditions of use and reuse
    Tell us how you'll use it by filling out this short form:
    👉 Fill out the API usage form This helps us understand real-world uses of the API and prioritize improvements.

How to Best Use the API#
General principles#

    You can search for product information, including many useful computed values.
    Suppose we don't have the information you need on a specific product. In that case, you (or your users) can upload the product photos, and the backend (and our AI algorithms!) will process them, generating helpful info. The photos will also be available for the users of Open Food Facts and every other API user.
    You could also ask your user to enter some of the information about the product (like name, category, and weight) so that they immediately get the computed info.

    Generally, the more information we have about a product, the more we can compute.

Rate limits#

To protect our infrastructure, we enforce rate-limits on the API and the website. The following limits apply:

    15 req/min/IP address for all read product queries (GET /api/v*/product requests or product page). There is no limit on product write queries.
    10 req/min/IP address for all search queries (GET /api/v*/search or GET /cgi/search.pl requests); don't use it for a search-as-you-type feature, you would be blocked very quickly.

If these limits are reached, we reserve the right to deny you access to the website and the API through IP address ban. If your IP has been banned, feel free to email us to explain why you reached the limits: reverting the ban is possible.

If your requests come from your users directly (ex: mobile app), the rate limits apply per user.

If you need to fetch more than a few hundred products, we ask you to download the data as a CSV or JSONL file directly. If you need to download images in bulk, we have a guide for that.

To protect our infrastructure from abusive crawl, we also added global rate-limits on the website and API endpoints, irrespective of the IP address. A HTTP 503 response (Service Not Available) will be returned if these limits are exceeded.

If you expect your app to generate a lot of API traffic, we strongly encourage you to host a local instance of Product Opener (or another custom API backend), and use the daily exports to update your local database.
If your users do not expect a result immediately (e.g., Inventory apps)#

    Submit photos (front packaging/nutrition values/ingredients): the most painless thing for your users.
    The backend (Product Opener) and Open Food Facts AI (Robotoff) will generate some derived data from the photos.
    Over time, other apps and the Open Food Facts community will fill the data gaps.

If your users expect a result immediately (e.g., Nutrition apps)#

    If you submit the product's nutritional values and category, you'll get the Nutri-Score.
    If you submit the product ingredients, you'll get the NOVA group (about food ultra-processing), additives, allergens, normalized ingredients, vegan, vegetarian…
    If you submit the product's category and labels, you'll get the Green-Score (a rating of the product's environmental impact)

API Deployments#

The OpenFoodFacts API has two deployments.

    Production: https://world.openfoodfacts.org
    Staging: https://world.openfoodfacts.net (need extra http basic auth with username off and password off, see below)

Consider using the staging environment if you are not in a production scenario.

While testing your applications, make all API requests to the staging environment. This way, we can ensure the product database is safe.

Staging require an http basic auth to avoid search engine indexing. The username is off, and the password off. Here is a small javascript code that you can test in your browser console:

fetch("https://world.openfoodfacts.net/api/v3.6/product/3274080005003.json", {
  method: "GET",
  headers: { Authorization: "Basic " + btoa("off:off") },
})
  .then((response) => response.json())
  .then((json) => console.log(json));

Authentication#

We ask you to always use a custom User-Agent to identify your app (to not risk being identified as a bot). The User-Agent should be in the form of AppName/Version (ContactEmail). For example, MyApp/1.0 (myapp@example.com).

    READ operations (getting info about a product, etc...) do not require authentication other than the custom User-Agent.
    WRITE operations (Editing an Existing Product, Uploading images…) require authentication. We do this as another layer of protection against spam.

Create an account on the Open Food Facts app for your app and fill out the API usage form so that we can identify your usage and prevent potential bans. From there, you have two options:

    The preferred one: Use the login API to get a session cookie and use this cookie for authentication in your subsequent requests. However, the session must always be used from the same IP address, and there's a limit on sessions per user (currently 10) with older sessions being automatically logged out to stay within the limit.
    If session conditions are too restrictive for your use case, include your account credentials as parameters for authenticated requests where user_id^[user_id_not_email] is your username and password is your password (do this on POST / PUT / DELETE requests, not on GET).

You can create a global account to allow your app users to contribute without registering individual accounts on the Open Food Facts website. This way, we know that these contributions came from your application.

We however ask that you send the app_name, app_version and app_uuid parameters in your write queries.

    app_name=MyApp
    app_version=1.1
    app_uuid=xxxx: a salted random uuid for the user so that Open Food Facts moderators can selectively ban any problematic user without banning your whole app account.

    Production and staging have different account databases, so the account you create in the production environment will only work for production requests. If you want to query (WRITE requests) the staging environment, you'll need to create another account there too.

    Note: Open Food Facts is migrating to Keycloak as its authentication system. OIDC-based authentication (OAuth 2.0 bearer tokens) will be supported in the future. The legacy username/password approach will remain available for backward compatibility.

^[user_id_not_email]: user_id is the username of your account. You must not use your email address.
Reference Documentation (OpenAPI)#

We are building a complete OpenAPI reference. Here is a list of the current API documentation available:

    OpenAPI documentation for v3 (current — latest sub-version v3.6)
    OpenAPI documentation (v2) (deprecated)
    A cheatsheet listing some common patterns.
    A change log for the API and product schema

Search#

Structured (filter-based) search by categories, brands, nutrients, and other fields is available in API v2 via the /api/v2/search endpoints.

    Note: Full-text / plain-text search is not available in the v2 server-side API. Full-text search will be available in Search-a-licious, which will provide a dedicated search API at search.openfoodfacts.org.

API v2 vs v3 — Feature availability#
Feature 	API v2 	API v3 	Notes
Read product 	✅ 	✅ 	
Write product 	✅ 	✅ 	
Structured search 	✅ /api/v2/search 	❌ Not available 	
Full-text search 	⚠️ Legacy /cgi/search.pl only 	❌ Not available 	Use Search-a-licious
Image upload 	✅ 	✅ /api/v3.3/product/*/images 	New structured endpoint since v3.3
Image selection 	✅ 	✅ (v3.3+) 	
Attribute groups 	✅ 	✅ /api/v3/attribute_groups 	Attributes with parameters excluded from v2 response (since v3.4)
Taxonomy suggestions 	✅ 	✅ /api/v3/taxonomy_suggestions 	
Tags sources 	❌ 	✅ tags_sources field 	New in v3.6; see change log
Tutorials#

    A comprehensive introduction to Using the Open Food Facts API.
    Uploading images to the Open Food Facts API

Help#

    Try the FAQ - to answer most of your questions.
    Didn't get what you wanted? Contact the Team on the #api Slack Channel.
    Report Bugs on the Open Food Facts GitHub repository.
    Do you have an issue or feature request? You can submit it on GitHub too.
    Are you interested in contributing to this project? See our Contribution Guidelines. {/ /}

SDKs#

SDKs are available for specific languages for easier use of the API. We may have a wrapper for your favourite programming language. If we do, you can use it and improve it. However, If we don't, you can help us create it!

They will let you consume data and let your users contribute new data. Open-source contributors develop our SDKs, and more contributions are welcome to improve these SDKs. You can start by checking the existing issues in their respective repositories.

    Warning: Before exploring any SDK, please read the Before You Start section.

    Also, remember to check the API Reference Documentation first to verify if the problem is in SDK implementation or in the API itself.

    Cordova: GitHub (old Open Food Facts official app)
    DART: GitHub - Package on pub.dev
    Elixir: GitHub - Discussion channel
    Go: GitHub - Discussion channel
    Java: GitHub - Discussion channel
    Spring Boot: GitHub - Discussion channel
    Kotlin: GitHub - Discussion channel
    NodeJS: GitHub - Discussion channel
    PHP: GitHub - Discussion channel
    PHP (Laravel): GitHub - Discussion channel
    Python: GitHub, published on pypi - Discussion channel
    React Native: GitHub - Discussion channel
    Ruby: GitHub - Discussion channel
    Rust: GitHub - Discussion channel
    R: GitHub - Discussion channel
    Swift: GitHub - Discussion channel
    .NET/C#: GitHub - Discussion channel


    Tutorial on using the Open Food Facts API#

Welcome to this tutorial on basic usage of Open Food Facts API.

First, be sure to see our Introduction to the API.
Scan A Product To Get Nutri-score#

This basic tutorial shows you can get the Nutri-score of a product, for instance, to display it in a mobile app after scanning the product barcode. Let's use Nutella Ferrero as the product example for this tutorial.

To get a product nutriscore, send a request to the Get A Product By Barcode endpoint.
Authentication#

Usually, no authentication is required to query Get A Product Nutri-score. However, there is a basic auth to avoid content indexation in the staging environment (which is used throughout this tutorial). For more details, visit the Open Food Facts API Environment.
Describing the Get Request#

Make a GET request to the Get A Product By Barcode endpoint.

https://world.openfoodfacts.net/api/v2/product/{barcode}

The {barcode} is the barcode number of the product you are trying to get. The barcode for Nutella Ferrero is 3017624010701. Then the request path to get product data for Nutella Ferrero will look like this:

https://world.openfoodfacts.net/api/v2/product/3017624010701

The response returns every data about Nutella Ferrero on the database. To get the nutriscore, we need to limit the response by specifying the nutriscore field, which is the nutrition_grades and product_name. {/ /}
Query Parameters#

To limit the response of the Get A Product By Barcode response, use query parameters to specify the product fields to be returned. In this example, you need one query parameter called field with the value product_name,nutrition_grades.

The request path will now look like this:

https://world.openfoodfacts.net/api/v2/product/3017624010701?fields=product_name,nutriscore_data

Nutri-Score Response#

The response returned contains an object of the code, product, status_verbose, and status. The product object contains the fields specified in the query: the product_name and the nutrition_grades. The status also states if the product was found or not.

{
    "code": "3017624010701",
    "product": {
        "nutrition_grades": "e",
        "product_name": "Nutella"
    },
    "status": 1,
    "status_verbose": "product found"
}

Nutri-Score Computation#

If you would like to be able to show how the score is computed, add some extra fields like nutriscore_data and nutriments.

The request path to get the Nutri-Score computation for Nutella-Ferrero will be :

https://world.openfoodfacts.net/api/v2/product/3017624010701?fields=product_name,nutriscore_data,nutriments,nutrition_grades

The product object in the response now contains the extra fields to show how the nutriscore was computed.

{
    "code": "3017624010701",
    "product": {
        "nutriments": {
            "carbohydrates": 57.5,
            "carbohydrates_100g": 57.5,
            "carbohydrates_unit": "g",
            "carbohydrates_value": 57.5,
            "energy": 2255,
            "energy-kcal": 539,
            "energy-kcal_100g": 539,
            "energy-kcal_unit": "kcal",
            ...,
            ...,
            "sugars": 56.3,
            "sugars_100g": 56.3,
            "sugars_unit": "g",
            "sugars_value": 56.3
        },
        "nutriscore_data": {
            "energy": 2255,
            "energy_points": 6,
            "energy_value": 2255,
            ...,
            ...,
            "sugars_points": 10,
            "sugars_value": 56.3
        },
        "nutrition_grades": "e",
        "product_name": "Nutella"
    },
    "status": 1,
    "status_verbose": "product found"
}

For more details, see the reference documentation for Get A Product By Barcode.

{/ /}
Completing products to get the Nutri-Score#
Products without a Nutri-Score#

When these fields are missing in a nutriscore computation response, it signifies that the product does not have a Nutri-Score computation due to some missing nutrition data. Let's look at the 100% Real Orange Juice. If the product nutrition data is missing some fields, you can volunteer and contribute to it by getting the missing tags and writing to the OFF API to add them.

{/ /}

To know the missing tags, check the misc-tags field from the product response.

https://world.openfoodfacts.net/api/v2/product/0180411000803/100-real-orange-juice?fields=misc_tags

The response shows the missing fields and category needed to compute the Nutri-Score.

{
    "code": "0180411000803",
    "product": {
        "misc_tags": [
            "en:nutriscore-not-computed",
            "en:nutriscore-missing-category",
            "en:nutrition-not-enough-data-to-compute-nutrition-score",
            "en:nutriscore-missing-nutrition-data",
            "en:nutriscore-missing-nutrition-data-sodium",
            "en:ecoscore-extended-data-not-computed",
            "en:ecoscore-not-computed",
            "en:main-countries-new-product"
        ]
    },
    "status": 1,
    "status_verbose": "product found"
}

The sample response above for 100% Real Orange Juice misc_tags shows that the Nutri-Score is missing category (en:nutriscore-missing-category) and sodium (salt) (en:nutriscore-missing-nutrition-data-sodium). Now you can write to the OFF API to provide these nutriment data (if you have them) so that the Nutri-Score can be computed.
Write data to make Nutri-Score computation possible#

The WRITE operations in the OFF API require authentication. Therefore you need a valid user_id^[user_id_not_email] and password to write the missing nutriment data to 100% Real Orange Juice.

    Sign up on the Open Food Facts App to get youruser_id and password if you don't have one.

To write data to a product, make a POST request to the Add or Edit A Product endpoint.

https://world.openfoodfacts.net/cgi/product_jqm2.pl

Add your valid user_id and password as body parameters to your request for authentication. The code (barcode of the product to be added/edited), user_id, and password are required when adding or editing a product. Then, include other product data to be added in the request body.

To write sodium and category to 100% Real Orange Juice so that the Nutri-Score can be computed, the request body should contain these fields:
Key 	Value 	Description
user_id 	*** 	A valid user_id
password 	*** 	A valid password
code 	0180411000803 	The barcode of the product to be added/edited
nutriment_sodium 	0.015 	Amount of sodium
nutriment_sodium_unit 	g 	Unit of sodium relative to the amount
categories 	Orange Juice 	Category of the Product
Using curl: 		

curl -XPOST -x POST https://world.openfoodfacts.net/cgi/product_jqm2.pl \
  -F user_id=your_user_id -F password=your_password \
  -F code=0180411000803 -F nutriment_sodium=0.015 -F nutriment_sodium_unit=g -F categories="Orange Juice"

If the request is successful, it returns a response that indicates that the fields have been saved.

{
    "status_verbose": "fields saved",
    "status": 1
}

^[user_id_not_email]: user_id is the username of your account. You must not use your email address.
Read newly computed Nutri-Score#

Now, let's check if the Nutri-Score for 100% Real Orange Juice has been computed now that we have provided the missing data. Make a GET request to https://world.openfoodfacts.net/api/v2/product/0180411000803?fields=product_name,nutriscore_data,nutriments,nutrition_grades for Nutri-Score of 100% Real Orange Juice. The response now contains the Nutri-Score computation:

{
    "code": "0180411000803",
    "product": {
        "nutriments": {
            "carbohydrates": 11.864406779661,
            .
            .
            .
            "sugars_unit": "g",
            "sugars_value": 11.864406779661
        },
        "nutriscore_data": {
            "energy": 195,
            "energy_points": 7,
            "energy_value": 195,
            .
            .
            .
            "sugars_value": 11.86
        },
        "nutrition_grades": "c",
        "product_name": "100% Real Orange Juice"
    },
    "status": 1,
    "status_verbose": "product found"
}

For more details, see the reference documentation for Add or Edit A Product

You can also check the reference cheatsheet to know how to add/edit other types of product data.

{/ /}
Search for a Product by Nutri-score#

Using the Open Food Facts API, you can filter products based on different criteria. To search for products in the Orange Juice category with a nutrition_grade of c, query the Search for Products endpoint.

Note: we describe here the v2 search API, but beware that only v1 search API supports full text search.
Describing the Search Request#

Make a GET request to the Search for Products endpoint.

https://world.openfoodfacts.org/api/v2/search

Add the search criteria used to filter the products as query parameters. For Orange Juice with a nutrition_grade of c, add query parameters categories_tags_en to filter Orange Juice while nutrition_grades_tags to filter c. The response will return all the products in the database with the category Orange Juice and nutrition_grade c.

https://world.openfoodfacts.net/api/v2/search?categories_tags_en=Orange Juice&nutrition_grades_tags=c

To limit the response, add fields to the query parameters to specify the fields to be returned in each product object response. For this tutorial, limit the response to code, product_name, nutrition_grades, and categories_tags_en.

https://world.openfoodfacts.net/api/v2/search?categories_tags_en=Orange Juice&nutrition_grades_tags=c&fields=code,nutrition_grades,categories_tags_en

The response returns all products that belong to the Orange Juice category, with the nutrition_grade "c" and limits each product object response to only the specified fields. It also returns the count(total number) of products that match the search criteria.

{
    "count": 1629,
    "page": 1,
    "page_count": 24,
    "page_size": 24,
    "products": [
        {
            "categories_tags_en": [
                "Plant-based foods and beverages",
                "Beverages",
                "Plant-based beverages",
                "Fruit-based beverages",
                "Juices and nectars",
                "Fruit juices",
                "Concentrated fruit juices",
                "Orange juices",
                "Concentrated orange juices"
            ],
            "code": "3123340008288",
            "nutrition_grades": "c"
        },
        .
        .
        .
        {
            "categories_tags_en": [
                "Plant-based foods and beverages",
                "Beverages",
                "Plant-based beverages",
                "Fruit-based beverages",
                "Juices and nectars",
                "Fruit juices",
                "Non-Alcoholic beverages",
                "Orange juices",
                "Squeezed juices",
                "Squeezed orange juices"
            ],
            "code": "3608580844136",
            "nutrition_grades": "c"
        }
    ],
    "skip": 0
}

Sorting Search Response#

You can proceed to also sort the search response by different fields, for example, sort by the product that was modified last or even by the product_name. Now, let's sort the products with Orange Juice and a nutrition_grade of "c" by when they were last modified. To sort the search response, add the sort_by with value last_modified_t as a query parameter to the request.

https://world.openfoodfacts.net/api/v2/search?nutrition_grades_tags=c&fields=code,nutrition_grades,categories_tags_en&categories_tags_en=Orange Juice&sort_by=last_modified_t

The date that each product was last modified is now used to order the product response.

{
    "count": 1629,
    "page": 1,
    "page_count": 24,
    "page_size": 24,
    "products": [
        {
            "categories_tags_en": [
                "Plant-based foods and beverages",
                "Beverages",
                "Plant-based beverages",
                "Fruit-based beverages",
                "Juices and nectars",
                "Fruit juices",
                "Orange juices"
            ],
            "code": "3800014268048",
            "nutrition_grades": "c"
        },
        '
        '
        '
        {
            "categories_tags_en": [
                "Plant-based foods and beverages",
                "Beverages",
                "Plant-based beverages",
                "Fruit-based beverages",
                "Juices and nectars",
                "Fruit juices",
                "Orange juices",
                "Squeezed juices",
                "Squeezed orange juices"
            ],
            "code": "4056489641018",
            "nutrition_grades": "c"
        }
    ],
    "skip": 0
}

To see other examples of sorting a search response, see the reference documentation for Search for Products.


Search Products
get /api/v2/search

Search request allows you to get products that match your search criteria.

It allows you create many custom APIs for your use case.

If a search query parameter has multiple values, separate them with commas (,). When filtering with a language-specific parameter (fr, de, en, etc.), specify the language code in the parameter name (for example: categories_tags_en).

Important: search API v2 does not support full text request (search_term), you have to use search API v1 for that. Upcoming search-a-licious project will fix that.
Limiting results

You can limit the size of returned objects thanks to the fields object (see below).

Example: fields=code,product_name,brands,attribute_groups

Please use it as much as possible to avoid overloading the servers.

The search endpoint uses pagination: see page and page_size parameters.

Beware: page_count is the number of products returned in the current page, not the total number of pages.
Conditions on tags

All `_tags`` parameters accepts either:

    a single value
    or a comma-separated list of values (doing a AND)
    or a pipe separated list of values (doing a OR)

You can exclude terms by using a "-" prefix.

For taxonomized entries, you might either use the tag id (recommended), or a known synonym (without language prefix)

    labels_tags=en:organic,en:fair-trade find items that are fair-trade AND organic
    labels_tags=en:organic|en:fair-trade find items that are fair-trade OR organic
    labels_tags=en:organic,en:-fair-trade find items that are organic BUT NOT fair-trade

Conditions on nutriments

To get a list of nutrients:

You can either query on nutrient per 100g (_100g suffix) or per serving (serving suffix).

You can also add _prepared_ to get the nutrients in the prepared product instead of as sold.

You can add a comparison operator and value to the parameter name to get products with nutrient above or below a value. If you use a parameter value it exactly match it.

    energy-kj_100g<200 products where energy in kj for 100g is less than 200kj
    sugars_serving>10 products where sugar per serving is greater than 10g
    saturated-fat_100g=1 products where saturated fat per 100g is exactly 10g
    salt_prepared_serving<0.1 products where salt per serving for prepared product is less than 0.1g

Combining filters and pagination (examples)

    Breakfast cereals with Nutri-Score A or B, with explicit pagination: /api/v2/search?categories_tags_en=breakfast-cereals&nutrition_grades_tags=a|b&page=2&page_size=20&fields=code,product_name,nutrition_grades

    Multiple constraints in one query (category + nutrients + sorting + compact fields): /api/v2/search?categories_tags_en=orange-juices&sugars_100g%3C8&salt_100g%3C0.2&sort_by=last_modified_t&page=1&page_size=24&fields=code,product_name,nutriments

    Include/exclude tags together: /api/v2/search?labels_tags=en:organic,en:-fair-trade&page=1&page_size=24&fields=code,product_name,labels_tags

Response structure at a glance

A successful response contains:

    count: total number of matching products
    page: current page number
    page_size: requested number of products per page
    page_count: number of products actually returned in this page
    skip: number of products skipped before this page
    products: array of products for the current page

Total page count can be computed with: Math.floor((count - 1) / page_size) + 1
More references

See also wiki page
API Key (User-Agent)
REQUEST
QUERY-STRING PARAMETERS
additives_tags
string
		
	

The additives_tags in english of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for additives. Translated values are available at https://static.openfoodfacts.org/data/taxonomies/additives.json

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: e322
allergens_tags
string
		
	

The allergens_tags in english of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for allergens. Translated values are available at https://static.openfoodfacts.org/data/taxonomies/allergens.json

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: m
brands_tags
string
		
	

The brands_tags of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for brands. Taxonomized values are available at https://static.openfoodfacts.org/data/taxonomies/brands.json

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: ferrero
categories_tags
string
		
	

The category of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for categories. Translated values are available at https://static.openfoodfacts.org/data/taxonomies/categories.json

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: chocolates
countries_tags_en
string
		
	

The countries_tags_en of product(s) you are searching for. The Open Food Facts Web App shows a list of possible values for countries. Translated values are available at https://static.openfoodfacts.org/data/taxonomies/countries.json

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: united-kingdom
emb_codes_tags
string
		
	

The emb_codes_tags of product(s) you are searching for.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
labels_tags
string
		
	

The labels_tags in english of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for labels.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: organic
manufacturing_places_tags
string
		
	

The manufacturing_places_tags of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for manufacturing-places.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
nutrition_grades_tags
string
		
	

The nutrition_grades_tags of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for nutrition-grades.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: e
origins_tags
string
		
	

The origins_tags of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for origins.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
packaging_tags_de
string
		
	

The packaging_tag in german of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for packaging.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: 1-jar-aus-klarglas
purchase_places_tags
string
		
	

The purchase_places_tags of product(s) you are searching for.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
states_tags
string
		
	

The states_tags in english of product(s) you are searching for. The Open Food Facts Web App has a list of possible values for states. Translated values are available at https://static.openfoodfacts.org/data/taxonomies/states.json You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: nutrition-facts-completed
stores_tags
string
		
	

The stores_tags of product(s) you are searching for.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
Examples: aldi
traces_tags
string
		
	

The traces_tags of product(s) you are searching for. The Open Food Facts Web App shows a list of possible values for traces.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
<tag_name>_tags_<language_code>
object
	
object
Multiline description
{
[pattern: (?<tag_name>\w+)_tags_(?<language_code>\w\w)]: string

Will search in the tags corresponding to tag_name, in the language corresponding to `language_code.

tag_name is one of the field above which have the `_tags`` suffix: categories, nutrition_grades, etc.

language_code is a two letter iso language `language_code.

You can use multiple values by using a comma separated list. You can add a "-" before values to avoid matching a tag.
}
	
	

You can add a language code to a specific tag to query it in a specific language
Examples: { packaging_tags_de:null, ... } ┃ { origins_tags_fr:null, ... } ┃ { categories_tags_en:null, ... }
<nutrient>_lt_<value>
object
	
object
Multiline description
{
[pattern: (?<nutrient>\w+)(?<prepared>_prepared)?(?<portion>100g|serving)<(?<value>\d+)]: string

Will search for products with nutrients lower than value per portion (100g or serving).

If prepared is "prepared" search in prepared product instead of "as sold".

Important: the parameter value is discarded and should be empty
}
	
	

Search on nutrient lower than a value
Examples: { salt_100g_lt_2:null, ... }
<nutrient>_gt_<value>
object
	
object
Multiline description
{
[pattern: (?<nutrient>\w+)(?<prepared>_prepared)?(?<portion>100g|serving)>(?<value>\d+)]: string

Will search for products with nutrients more than value per portion (100g or serving).

If prepared is "prepared" search in prepared product instead of "as sold".

Important: the parameter value is discarded and should be empty
}
	
	

Search on nutrient greater than a value
Examples: { carbohydrates_prepared_serving_gt_10:null, ... }
<nutrient>_eq_<value>
object
	
object
Multiline description
{
[pattern: (?<nutrient>\w+)(?<prepared>_prepared)?(?<portion>100g|serving)]: string

Will search for products with nutrients exactl the parameter value per portion (100g or serving).

If prepared is "prepared" search in prepared product instead of "as sold".
}
	
	

Search on nutrient for an exact quantity
Examples: { fat_100g_eq_5:null, ... }
fields
string
		
Pattern: ^[a-zA-Z0-9_.-]+(,[a-zA-Z0-9_.-]+)*$
	

Specific fields to return. Use 'knowledge_panels' for Knowledge Panels only.
Examples: knowledge_panels ┃ knowledge_panels,product_name,nutriments ┃ categories_tags,packaging_tags ┃ allergens_tags,nutriscores ┃ images,brands_tags,categories_tags,countries_tags,languages_tags,ecoscore_tags
sort_by
enum
		
Allowed: product_name ┃ last_modified_t ┃ scans_n ┃ unique_scans_n ┃ created_t ┃ completeness ┃ popularity_key ┃ nutriscore_score ┃ nova_score ┃ nothing ┃ ecoscore_score
	

The allowed values used to sort/order the search results.

    product_name sorts on name
    ecoscore_score, nova_score, nutriscore_score rank on the Eco-Score, Nova, or Nutri-Score
    scans_n, unique_scans_n and popularity_key are about product popularity: number of scans on unique scans, rank of product
    created_t, last_modified_t, are about creation and modification dates
    nothing, tells not to sort at all (because if you do not provide the sort_by argument we default to sorting on popularity (for food) or last modification date)

Examples: product_name
page
integer
		
	

The page number you request to view (eg. in search results spanning multiple pages)
Examples: 24
page_size
integer
		
	

The number of elements should be sent per page
Examples: 24
API Server
https://world.openfoodfacts.net
Authentication
Required (None Applied)
RESPONSE
200

OK
application/json
object
Multiline description

search_for_products_response
{
count: integer

Total number of products found
page: integer

Page number of returned results.

You can get a different page, by using the page query parameter.
page_count: integer

Number of products returned in the current page.

This will differ from page_size only on the last page.
page_size: integer

Requested number of products per page.

To get the number of pages, compute: Math.floor((count - 1) / page_size) + 1
products: [{

The products matching the query corresponding to current page
⮕ [ This is all the fields describing a product and how to display it on a page.

Refer to the different sub schema for more readable entries:

    Product Base: Base fields of a product
    Product Misc: Miscellaneous but important fields of a product
    Product Tags: Tags fields on a product
    Product Images: Information about Images of a product
    Product Eco-Score: Fields related to Eco-Score for a product
    Product Ingredients: Fields about ingredients of a product
    Product Nutrition: Nutrition fields of a product
    Product Data Quality: fields related to data quality for a product
    Product Extended: TODO
    Product Metadata: Metadata of a product (author, editors, etc.)
    Product Knowledge Panels: Knowledge panels for a product
    Product Attribute Groups: Attribute groups for personal product matching ]

product_type: enum

The product type is a fundamental separation that tells on which platform the product is made available: Open Food Facts, Open Beauty Facts, Open Pet Food Facts or Open Products Facts.

Each platform has variations on the way it analyses the product.

Changing the product type moves the product to the new platform. It must be done thoughtfully.
Allowed: beauty┃food┃petfood┃product
product_name: string

The name of the product in the main language of the product (lang field)
abbreviated_product_name: string

Abbreviated product name (e.g. abbreviated product name printed on receipts) in the main language of the product (lang field)
code*: string

barcode of the product (can be EAN-13 or internal codes for some food stores), for products without a barcode, Open Food Facts assigns a number starting with the 200 reserved prefix
codes_tags: [string]

⮕ [ A value which is the type of barcode "code-13" or "code-8" and A series of mask for the barcode It helps retrieve barcodes starting by ]
generic_name: string

Generic / legal name of the product in the main language of the product (lang field)
id: string

internal identifier for the product, usually set to the value of code, except on the producers platform where it is prefixed by the owner
lc: string

Main language of the product. This is a duplicate of lang property (for historical reasons).
lang: string

Main language of the product.

This should be the main language of product packaging (if one is predominant).

Main language is also used to decide which ingredients list to parse.
nova_group: integer

Nova group as an integer from 1 to 4. See https://world.openfoodfacts.org/nova
nova_groups: string
obsolete: string
obsolete_since_date: string

A date at which the product was declared obsolete. This means it's not produced any more.
product_quantity: string

The size in g or ml for the whole product. It is a normalized version of the quantity field. A quantity of "2 x 60 g" leads to product_quantity: "120".
product_quantity_unit: string

The unit (either g or ml) for the corresponding product_quantity. It is computed from the quantity field. A quantity of "6 x 250 ml" leads to product_quantity_unit: "ml".
quantity: string

The quantity of the product, with the corresponding number of portions or unit (g, ml, kg, l, cl, oz, lbs...). It should be the value as displayed on the product. The ℮ sign is allowed. When it refers to the number of portions, it can be filled without any units (e.g. "6 eggs").
schema_version*: integer

Version of the product object schema used in the response. This indicates the structure of the 'product' field itself. For more details, please read: https://openfoodfacts.github.io/openfoodfacts-server/api/ref-api-and-product-schema-change-log
additives_n: integer

Number of food additives.
checked: string
complete: integer
completeness: number
ecoscore_grade: string

See also: ecoscore_tags
ecoscore_score: integer

See also: ecoscore_tags
food_groups: string
food_groups_tags: [string]
nutrient_levels: {

Traffic light indicators on main nutrients levels
fat: enum
Allowed: low┃moderate┃high
salt: enum
Allowed: low┃moderate┃high
saturated-fat: enum
Allowed: low┃moderate┃high
sugars: enum
Allowed: low┃moderate┃high
}
packaging_text: string

Recycling instructions as raw text, e.g. Plastic bottle to recycle, Plastic cap to recycle. This will get automatically parsed and will be used to compute the Eco-Score. You can either request it (if it exists) or send it in a specific language.
packagings 🆁: [{

Packagings (READ): The packagings object is an array of individual packaging component objects.

The Packaging data document explains how packaging data is structured in Open Food Facts: https://openfoodfacts.github.io/openfoodfacts-server/dev/explain-packaging-data/

The shape, material and recycling properties of each packaging component are linked to entries in the packaging_shapes, packaging_materials and packaging_recycling taxonomies:

https://world.openfoodfacts.org/data/taxonomies/packaging_shapes.json https://world.openfoodfacts.org/data/taxonomies/packaging_materials.json https://world.openfoodfacts.org/data/taxonomies/packaging_recycling.json

If the tags_lc field is set, the properties will include a lc_name field with the translation in the requested language.
⮕ [ Each packaging component has different properties to specify how many there are, its shape, material etc.

The shape, material and recycling properties are mapped to one entry in the packaging_shapes, packaging_materials and packaging_recycling taxonomies, and the value of the property is the canonical name of the taxonomy entry (e.g. en:bottle).

They may contain values that could not yet get matched to their respective taxonomy, in which case they will contain a free text value prefixed with the language code of this text value (e.g. "fr:Bouteille sphérique" might have been entered by a French user to indicate it is a spherical bottle). ]
number_of_units: integer

umber of units of this packaging component contained in the product (e.g. 6 for a pack of 6 bottles)
shape: {

Packaging component shape: The shape property is canonicalized using the packaging_shapes taxonomy. Taxonomized values are available using the partial taxonomy API, the autosuggest API or the full packaging_shapes taxonomy JSON export.
id: string

Canonical id of the entry in the taxonomy. If the value cannot be mapped to a taxonomy entry, the value will be the name of the entry in its original language prefixed by the language 2 letter code and a colon.
lc_name: string

Name of the entry in the language requested in the tags_lc field of the request. This field is returned only of tags_lc is specified. If the translation is not available, or if the entry does not exist in the taxonomy, the value will be the name of the entry in its original language prefixed by the language 2 letter code and a colon.
}
material: {

Packaging component material: The material property is canonicalized using the packaging_materials taxonomy. Taxonomized values are available using the partial taxonomy API, the autosuggest API or the full packaging_material taxonomy JSON export.
id: string

Canonical id of the entry in the taxonomy. If the value cannot be mapped to a taxonomy entry, the value will be the name of the entry in its original language prefixed by the language 2 letter code and a colon.
lc_name: string

Name of the entry in the language requested in the tags_lc field of the request. This field is returned only of tags_lc is specified. If the translation is not available, or if the entry does not exist in the taxonomy, the value will be the name of the entry in its original language prefixed by the language 2 letter code and a colon.
}
recycling: {

Packaging component recycling instruction: The recycling property is canonicalized using the packaging_recycling taxonomy. Taxonomized values are available using the partial taxonomy API, the autosuggest API or the full packaging_recycling taxonomy JSON export.
id: string

Canonical id of the entry in the taxonomy. If the value cannot be mapped to a taxonomy entry, the value will be the name of the entry in its original language prefixed by the language 2 letter code and a colon.
lc_name: string

Name of the entry in the language requested in the tags_lc field of the request. This field is returned only of tags_lc is specified. If the translation is not available, or if the entry does not exist in the taxonomy, the value will be the name of the entry in its original language prefixed by the language 2 letter code and a colon.
}
quantity_per_unit: string

Quantity (weight or volume) of food product contained in the packaging component. (e.g. 75cl for a wine bottle)
quantity_per_unit_value: number

Value parsed from the quantity field.
quantity_per_unit_unit: string

Unit parsed and normalized from the quantity field.
weight_specified: number

Weight (as specified by the manufacturer) of one unit of the empty packaging component (in grams). (e.g. for a 6 pack of 1.5l water bottles, it might be 30, the weight in grams of 1 empty water bottle without its cap which is a different packaging component).
weight_measured: number

Weight (as measured by one or more users) of one unit of the empty packaging component (in grams). (e.g. for a 6 pack of 1.5l water bottles, it might be 30, the weight in grams of 1 empty water bottle without its cap which is a different packaging component).
weight_estimated: number

Weight (as estimated from similar products) of one unit of the empty packaging component (in grams). (e.g. for a 6 pack of 1.5l water bottles, it might be 30, the weight in grams of 1 empty water bottle without its cap which is a different packaging component).
weight: number

Weight of one unit of the empty packaging component.
weight_source_id: string

Indicates which field was used to populate the "weight" field. Either "specified", "measured", or "estimated"
}]
packagings_complete: integer

packagings_complete: Indicate if the packagings array contains all the packaging parts of the product. This field can be set by users when they enter or verify packaging data. Possible values are 0 or 1.
Constraints: Min 0┃Max 1
pnns_groups_1: string

Category of food according to French Nutrition and Health Program
pnns_groups_1_tags: [string]
pnns_groups_2: string

Sub Category of food according to French Nutrition and Health Program
pnns_groups_2_tags: [string]
popularity_key: integer

An imprecise measurement of popularity based on Scan statistics. A higher value means higher popularity.
popularity_tags: [string]

Indicators for the popularity of a product, like the amount of scans in a specific year. popularity_tags values from previous years are kept, if there is no popularity_tags at all, then it was never popular. This field must be used with care. In countries where Open Food Facts is not widely used, a product may obtain the value "top-90-percent-scans-2021" with a single scan.
scans_n: integer

Number of scans performed with the official Open Food Facts mobile application, the last year for which the product was scanned (current year excluded). This value is computed once a year by scanbot.pl. It is possible that a product has never been scanned at all: scans_n is not given in this case. See also unique_scans_n field.
unique_scans_n: integer

Number of unique scans performed with the official Open Food Facts mobile application, the last year for which the product was scanned (current year excluded). Unique scans means it is based on different IPs. This value is computed once a year by scanbot.pl. See also scans_n field.
serving_quantity: string

Normalized version of serving_size. Note that this is NOT the number of servings by product. (in perl, see normalize_serving_size)
serving_quantity_unit: string

The unit (either g or ml) for the correponding serving_quantity.
serving_size: string

Serving size text (generally in g or ml). We expect a quantity + unit but the user is free to input any string.
brands: string

Comma separated list of brands (not taxonomized), in the last language used to edit it (recorded in brands_lc) This field is mostly used for debugging and testing purposes. Do not use it for display purposes.
brands_tags: [string]

An array of indexed brands tag entries (for search).

That is the id of brands found in taxonomy + brands not found in taxonomy (with case / accents / spaces normalized).

This is mostly used for search as the normalization of entries not in the taxonomy is lossy.
⮕ [ This field is used for search only. It is a lossy representation of the taxonomy tag entry, that can be used to index the entry in a search engine. A tag entry, that has been matched against a taxonomy (e.g. a category, a label)

The entry is a string that can contain either:

    a taxonomy entry id, in the form [2 letter language code]:[normalized canonical name] (e.g. "en:green-teas") -> for entries that could be matched to a taxonomy entry
    a string in a specific language, prefixed by the 2 letter language code, and normalized (deaccented and lowercased, depending on language) (e.g. "fr:thes-verts") -> for entries that could not be matched to a taxonomy entry ]

categories: string

Comma separated list of categories (not taxonomized), in the last language used to edit it (recorded in categories_lc) This field is mostly used for debugging and testing purposes. Do not use it for display purposes.
categories_tags: [string]

An array of indexed categories tag entries (for search).

That is the id of categories found in taxonomy + categories not found in taxonomy (with case / accents / spaces normalized).

This is mostly used for search as the normalization of entries not in the taxonomy is lossy.
⮕ [ This field is used for search only. It is a lossy representation of the taxonomy tag entry, that can be used to index the entry in a search engine. A tag entry, that has been matched against a taxonomy (e.g. a category, a label)

The entry is a string that can contain either:

    a taxonomy entry id, in the form [2 letter language code]:[normalized canonical name] (e.g. "en:green-teas") -> for entries that could be matched to a taxonomy entry
    a string in a specific language, prefixed by the 2 letter language code, and normalized (deaccented and lowercased, depending on language) (e.g. "fr:thes-verts") -> for entries that could not be matched to a taxonomy entry ]

checkers_tags: [string]

⮕ [ List of checkers (users who checked the product) tags ]
cities: string
cities_tags: [{
}]
correctors_tags: [string]
countries: string

List of countries where the product is sold.
countries_tags: [string]
ecoscore_tags: [string]

All ecoscore of a product. Most of the time it's only one value, but it might eventually be more for products composed of sub-products. See also: ecoscore_score, ecoscore_grade.
emb_codes: string

Packager code. EMB is the French system of traceability codes for packager.
emb_codes_tags: [{
}]
labels: string
labels_tags: [string]
entry_dates_tags: [string]

The data as a series of tag: yyyy-mm-dd, yyyy-mm, yyyy
manufacturing_places: string

Places where the product was manufactured or transformed.
manufacturing_places_tags: [string]
nova_groups_tags: [string]
nutrient_levels_tags: [string]
brands_hierarchy: [string]

An array of brands tag entries (for display and editing).

That is the id of brands found in taxonomy + brands not found in taxonomy (as-is, with no normalization).

This is the field that should be used for display purposes, as it is not lossy.
⮕ [ A tag entry, that has been matched against a taxonomy (e.g. a category, a label)

The entry is a string that can contain either:

    a taxonomy entry id, in the form [2 letter language code]:[normalized canonical name] (e.g. "en:green-teas") -> for entries that could be matched to a taxonomy entry
    a string in a specific language, prefixed by the 2 letter language code (e.g. "fr:Thés verts") -> for entries that could not be matched to a taxonomy entry ]

brands_lc: string

Language code of the last edit for brands
categories_hierarchy: [string]

An array of categories tag entries (for display and editing).

That is the id of categories found in taxonomy + categories not found in taxonomy (as-is, with no normalization).

This is the field that should be used for display purposes, as it is not lossy.
⮕ [ A tag entry, that has been matched against a taxonomy (e.g. a category, a label)

The entry is a string that can contain either:

    a taxonomy entry id, in the form [2 letter language code]:[normalized canonical name] (e.g. "en:green-teas") -> for entries that could be matched to a taxonomy entry
    a string in a specific language, prefixed by the 2 letter language code (e.g. "fr:Thés verts") -> for entries that could not be matched to a taxonomy entry ]

categories_lc: string

Language code of the last edit for categories
countries_hierarchy: [string]
countries_lc: string

Countries language code
emb_codes_orig: string
labels_hierarchy: [string]
labels_lc: string
images: {

product_images_properties: This contains properties for all images contained on the product.
front: {

image_role: property of an image (or part thereof) selected for a particular role and a particular language.
angle: integer

The angle of the image rotation (if it was rotated).
coordinates_image_size: string
geometry: string
imgid: string

The id of the original/source image that was selected to edit(rotate, normalize etc) to produce this new image.
normalize: string┃boolean┃null

Normalize colors.
rev: string
sizes: {

The available image sizes for the product (both reduced and full). The reduced images are the ones with numbers as the key( 100, 200 etc) while the full images have full as the key.
100: {...}

image_size: Width and height of an image
200: {...}

image_size: Width and height of an image
400: {...}

image_size: Width and height of an image
full: {...}

image_size: Width and height of an image
}
white_magic: string┃boolean┃null

Photo on white background : Try to remove the background.
x1: string
x2: string
y1: string
y2: string
}
[pattern: (?<imgid>\d+)]: string

See property 1 to get the real type of those objects (Put this way because of a bug in rapidoc)
[pattern: (?<image_type>front|nutrition|ingredients|packaging|other)_(?<language_code>\w\w)]: string

See property front to get the real type of those objects (Put this way because of a bug in rapidoc)
[any-key]: {

image: This object represent an image that was uploaded to a product. "imgid" is an integer which is a sequential number unique to each picture.
imgid: integer

If the image is selected as the front / ingredients / nutrition / packaging image, this is the index to access the original image URL in the images field of the product. If the image is not selected as one of these, this field is null.

So if the product has the following images field:

"images": {
  1: { ... },
  front_en: { imgid: 1, ... },
  ...
}

then the front image can be accessed by images[1].
sizes: {

The available image sizes for the product (both reduced and full). The reduced images are the ones with numbers as the key( 100, 200 etc) while the full images have full as the key.
full: {...}

image_size: Width and height of an image
[pattern: (?<image_size>100|400)]: string

properties of thumbnail of size image_size. TODO explain how to compute name

For real type: see description of property full. (Put this way because of a bug in rapidoc)
}
uploaded_t: string

The time the image was uploaded (as unix timestamp).
uploader: string

The contributor that uploaded the image.
}
}
last_image_dates_tags: [string]
last_image_t: integer

timestamp of last image upload (or update?)
selected_images: {

product_selected_images: URL for selected (important) images of the product.

This is very handy if you display the product to users.
front: {

URLs of thumbnails image of image of type image_type
display: {

Thumbnail urls of product image (front) adapted to display on product page
[pattern: (?<language_code>\w\w)]: string

url of the image for language language_code
}
small: {

Thumbnail urls of product image (front) adapted to display on product list page
[pattern: (?<language_code>\w\w)]: string

url of the image for language language_code
}
thumb: {

Thumbnail urls of product image (front) in smallest format
[pattern: (?<language_code>\w\w)]: string

url of the image for language language_code
}
}
[pattern: (?<image_type>front|packaging|ingredients|nutrition|other)]: string

See property front to get the real type of those objects (Put this way because of a bug in rapidoc)
}
image_small_url: string
image_thumb_url: string
image_url: string
ecoscore_data: {

product_ecoscore_data: An object about a lot of details about data needed for Eco-Score computation and complementary data of interest.
adjustments: {

product_ecoscore_adjustments
origins_of_ingredients: {
aggregated_origins: [{...}]

List of aggregated origins of ingredients and their impact.
epi_score: integer

Environmental Performance Index (EPI) score for the ingredient's origin.
epi_value: integer
origins_from_categories: [string]

List of origins derived from product categories, eg for PDO.
origins_from_origins_field: [string]

List of origins explicitly stated in the product's origins field.
transportation_score: integer
transportation_scores: {...}
transportation_value: integer
transportation_values: {...}
value: integer
values: {...}
warning: string
}
packaging: {
non_recyclable_and_non_biodegradable_materials: integer
packagings: [{...}]
score: integer
value: integer
warning: string
}
production_system: {
labels: [string]
value: integer
warning: string
}
threatened_species: {
ingredient: string
value: integer
}
}
agribalyse: {
agribalyse_proxy_food_code: string
agribalyse_food_code: string
co2_agriculture: number
co2_consumption: integer
co2_distribution: number
co2_packaging: number
co2_processing: number
co2_total: number
co2_transportation: number
code: string
dqr: string
ef_agriculture: number
ef_consumption: integer
ef_distribution: number
ef_packaging: number
ef_processing: number
ef_total: number
ef_transportation: number
is_beverage: integer
name_en: string

This can be returned in many other languages like name_fr (for french).
score: integer
version: string
}
grade: string
grades: {
[pattern: (?<country_code>\w\w)]: string
}
missing: {
labels: integer
origins: integer
packagings: integer
}
missing_data_warning: integer
previous_data: {
grade: string or null
score: integer or null
agribalyse: {
agribalyse_proxy_food_code: string
agribalyse_food_code: string
co2_agriculture: number
co2_consumption: integer
co2_distribution: number
co2_packaging: number
co2_processing: number
co2_total: number
co2_transportation: number
code: string
dqr: string
ef_agriculture: number
ef_consumption: integer
ef_distribution: number
ef_packaging: number
ef_processing: number
ef_total: number
ef_transportation: number
is_beverage: integer
name_en: string

This can be returned in many other languages like name_fr (for french).
score: integer
version: string
}
}
score: integer
scores: {
[pattern: (?<country_code>\w\w)]: integer
}
status: string
}
ecoscore_extended_data_version: string
environment_impact_level: string
environment_impact_level_tags: [{
}]
additives_tags: [string]
allergens: string

comma separated list of allergens
allergens_lc: string

language in which allergens where input
allergens_hierarchy: [string]
allergens_tags: [string]
ingredients: [{

This structure gives the different ingredients and some information about them, like estimate on their quantity.
id: string
ingredients: [{recursive: ingredient.yaml} ]

This structure gives the different ingredients and some information about them, like estimate on their quantity.
percent: integer

The percentage of the ingredient.
percent_estimate: number

Estimated percentage of the ingredient.
percent_max: string or number

Maximum percentage of the ingredient.
percent_min: integer

Minimum percentage of the ingredient.
text: string

Text description of the ingredient.
vegan: string

Indicates if the ingredient is vegan.
vegetarian: string

Indicates if the ingredient is vegetarian.
}]
ingredients_analysis: {
en:palm-oil: [string]
en:vegan-status-unknown: [string]
en:vegetarian-status-unknown: [string]
}
ingredients_analysis_tags: [string]
ingredients_from_or_that_may_be_from_palm_oil_n: integer
ingredients_from_palm_oil_n: integer
ingredients_from_palm_oil_tags: [{
}]
ingredients_hierarchy: [string]
ingredients_n: integer
ingredients_n_tags: [string]
ingredients_original_tags: [string]
ingredients_percent_analysis: integer

Indicates the result of ingredients analysis processing

    not present -> we didn't run ingredient percent analysis (e.g. we have no ingredients)
    1: we estimated the ingredients percent.
    -1 : we tried to estimate the ingredients, but the values were impossible. (e.g. if the sum of % is above 100%)

ingredients_sweeteners_n: integer

Number of sweeteners additives in the ingredients. Undefined if ingredients are not specified.
ingredients_non_nutritive_sweeteners_n: integer

Number of non-nutritive sweeteners additives (as specified in the Nutri-Score formula) in the ingredients. Undefined if ingredients are not specified.
ingredients_tags: [string]
ingredients_lc: string

Language that was used to parse the ingredient list. If ingredients_text is available for the product main language (lang), ingredients_lc=lang, otherwise we look at ingredients_text fields for other languages and set ingredients_lc to the first non-empty ingredient_text.
ingredients_text: string

Raw list of ingredients. This will get automatically parsed and get used to compute the Eco-Score or find allergens, etc..

It's a copy of ingredients_text in the main language of the product (see lang proprety).
ingredients_text_with_allergens: string

Same text as ingredients_text but where allergens have HTML elements around them to identify them
ingredients_that_may_be_from_palm_oil_n: integer
ingredients_that_may_be_from_palm_oil_tags: [{
}]
ingredients_with_specified_percent_n: integer
ingredients_with_specified_percent_sum: integer
ingredients_with_unspecified_percent_n: integer
ingredients_with_unspecified_percent_sum: integer
known_ingredients_n: integer
origins: string

Origins of ingredients
origins_hierarchy: [{
}]
origins_lc: string
origins_tags: [{
}]
traces: string

List of substances that might cause allergies that are present in trace amounts in the product (this does not include the ingredients, as they are not only present in trace amounts). It is taxonomized with the allergens taxonomy. Refer to the allergens taxonomy
traces_hierarchy: [{
ONE OF
1 {
}
2 string
}]
traces_lc: string
traces_tags: [{
ONE OF
1 {
}
2 string
}]
unknown_ingredients_n: integer
no_nutrition_data: string

When a product does not have nutrition data displayed on the packaging, the user can check the field "Nutrition facts are not specified on the product". By doing so, the no_nutrition_data field takes the value "on". This case is frequent (thousands of products).
nutrition_data_per: enum

The nutrition data on the package can be per serving or per 100g.

This is essential to understand if <nutrient>_value and <nutrient> values in nutriments applies for a serving or for 100g.

IMPORTANT: When writing products, this setting applies to all existing nutrients values for the product, not only the nutrient values sent in the write request. So it should not be changed unless all nutrients values are provided with values that match the nutrition_data_per field.
Allowed: serving┃100g
nutrition_data_prepared_per: enum

The nutrition data for prepared product on the package (if any) can be per serving or per 100g.

This is essential to understand if <nutrient>_prepared_value and <nutrient>_prepared values in nutriments applies for a serving or for 100g.

See also important note on nutrition_data_per.
Allowed: serving┃100g
nutriments: {

All known nutrients for the product.

Note that each nutrients are declined with a variety of suffixes like _100g, _serving, see patternProperties below.

A specific _unit is the unit of the value that was entered by the contributor (a user or the manufacturer). It is not necessarily the unit that is printed on the product, as some apps may force specific units when writing nutrient values.

Beware that some properties are to be interpreted based upon nutrition_data_per value.

For most use cases, you should use the <nutrient>_100g or <nutrient>_serving fields, as they are always in the same standard unit, for a specific quantity (100g or 1 serving).

For products that have a nutrition table for prepared product (eg. the nutrition facts for a bowl of milk with cocoa powder), a _prepared suffix is added (before other suffixes).

You can get all possible nutrients from the nutrients taxonomy

FIXME add more nutrients with description.
alcohol: number

Quantity of alcohol

(per 100g or per serving) in a standard unit (g or ml)
carbohydrates: number

This is the available carbohydrates (excluding fiber), also known as net carbohydrates
carbohydrates-total: number

This follows the US / Canada definition of carbohydrates which includes fiber, also known as gross carbohydrates
energy: number

It is the same as energy-kj if we have it, or computed from energy-kcal otherwise

(per 100g or per serving) in kj
energy_value: number

energy_value will be equal to energy-kj_value if we have it or to energy-kcal_value otherwise
energy_unit: enum

Equal to energy-kj_unit if we have it or to energy-kcal_unit otherwise
Allowed: kcal┃kJ
energy-kcal: number

energy in kcal, if it is specified

(per 100g or per serving) in a standard unit (g or ml)
energy-kj: number

energy in kj, if it is specified

(per 100g or per serving) in a standard unit (g or ml)
fat: number
fruits-vegetables-legumes-estimate-from-ingredients: number

An estimate, from the ingredients list of the percentage of fruits, vegetable and legumes. This is an important information for Nutri-Score (2023 version) computation.
fruits-vegetables-nuts-estimate-from-ingredients: number

An estimate, from the ingredients list of the percentage of fruits, vegetable and nuts. This is an important information for Nutri-Score (2021 version) computation.
nova-group: integer
nutrition-score-fr: {missing-type-info}

Experimental nutrition score derived from the UK FSA score and adapted for the French market (formula defined by the team of Professor Hercberg).
proteins: number
salt: number
saturated-fat: number
sodium: number
sugars: number
carbon-footprint-from-known-ingredients_product: integer
carbon-footprint-from-known-ingredients_serving: number
erythritol: number

erythritol is a polyol which is not providing any energy. As such, it needs not be taken into account when computing the energy of a product. Eryhtritol is now displayed on nutrition facts sheet of some products, mainly in the USA. This value is entered either by contributors, either by imports.
[pattern: (?<nutrient>[\w-]+)_unit]: enum

The unit of the value entered by the contributor (a user or the manufacturer), for the product as sold (not prepared). In most cases, this unit is the unit displayed on the product for the nutrient, but it is not guaranteed, as some apps may force specific units when writing nutrient values.

The possible values depends on the nutrient.

    g for grams
    mg for milligrams
    μg for micrograms
    cl for centiliters
    ml for mililiters
    dv for recommended daily intakes (aka Dietary Reference Intake)
    % vol for alcohol vol per 100 ml

🤓 code: see the Units module, and Food:default_unit_for_nid function

Note: For most use cases, you should not use this unit, as it can change depending on how the value was entered. Instead use the <nutrient>_100g or <nutrient>_serving fields, and convert them to the unit you need.
Allowed: 公斤┃公升┃kg┃кг┃l┃л┃毫克┃mg┃мг┃mcg┃µg┃oz┃fl oz┃dl┃дл┃cl┃кл┃斤┃g┃∅┃●┃kJ┃克┃公克┃г┃мл┃ml┃mmol/l┃毫升┃% vol┃ph┃%┃% dv┃% vol (alcohol)┃iu┃mol/l┃mval/l┃ppm┃�rh┃�fh┃�e┃�dh┃gpg
[pattern: (?<nutrient>[\w-]+)_100g]: number 🆁

The normalized value of the nutrient for 100g (or 100ml for liquids), in a standard unit, for the product as sold (not prepared):

    g for all nutrients that are measured by weight (e.g. fat, proteins, carbohydrates, sodium, vitamins, minerals etc.)
    % vol for nutrients that are percentages by volume (e.g. alcohol)
    kJ for energy and energy-kj
    kcal for energy-kcal
    no unit for pH

This is computed from the nutrient property, the serving size (if nutrient is per serving), and the nutrient_unit field.

Note: For most use cases, this is the only value you should use, as it is always in the same unit, and always per 100g or 100ml.
[pattern: (?<nutrient>[\w-]+)_serving]: number 🆁

The normalized value of the nutrient for one serving, in a standard unit:

    g for most nutrients
    % for nutrients that are percentages (e.g. alcohol)
    kJ for energy and energy-kj
    kcal for energy-kcal

This is computed from the nutrient property, the serving size (if nutrient is per 100g), and the nutrient_unit field.
[pattern: (?<nutrient>[\w-]+)_value]: number 🆁

The value entered by the contributor (a user or the manufacturer). In most cases, the unit of the value is the unit displayed on the product for the nutrient, but it is not guaranteed, as some apps may force specific units when writing nutrient values.

    per 100g or serving, depending on nutrition_data_per field.
    in the unit of the corresponding _unit field.

Note: For most use cases, you should not use this value, as it can be in different units, and can be per 100g or per serving.
[pattern: (?<nutrient>[\w-]+)_prepared]: number

Same as '(?[\w-]+)', for the prepared product (not as sold).
[pattern: (?<nutrient>[\w-]+)_prepared_unit]: string

Same as '(?[\w-]+)_unit', for the prepared product (not as sold).
[pattern: (?<nutrient>[\w-]+)_prepared_100g]: number 🆁

Same as '(?[\w-]+)_100g', for the prepared product (not as sold).
[pattern: (?<nutrient>[\w-]+)_prepared_serving]: number 🆁

Same as '(?[\w-]+)_serving', for the prepared product (not as sold).
[pattern: (?<nutrient>[\w-]+)_prepared_value]: number 🆁

Same as '(?[\w-]+)_value', for the prepared product (not as sold).
}
nutrition_grade_fr: string

Nutrition grade (‘a’ to ‘e’), https://world.openfoodfacts.org/nutriscore.
nutrition_grades: string

Nutrition grades as a comma separated list.

Some products with multiple components might have multiple Nutri-Score
nutrition_grades_tags: [string]
nutrition_score_beverage: integer
nutrition_score_warning_fruits_vegetables_nuts_estimate_from_ingredients: integer
nutrition_score_warning_fruits_vegetables_nuts_estimate_from_ingredients_value: integer
nutrition_score_warning_no_fiber: integer
other_nutritional_substances_tags: [{
}]
unknown_nutrients_tags: [{
}]
vitamins_tags: [{
}]
nutriscore: {

Nutriscores
2021: {

Nutriscore2021
category_available: integer
Allowed: 0┃1
grade: enum

Nutri-Score for the product as a letter.

See https://world.openfoodfacts.org/nutriscore.
Allowed: a┃b┃c┃d┃e
nutrients_available: integer
Allowed: 0┃1
nutriscore_applicable: integer
Allowed: 0┃1
nutriscore_computed: integer
Allowed: 0┃1
score: integer
data: {

Nutriscore2021Data
is_beverage: integer
Allowed: 0┃1
is_cheese: integer
Allowed: 0┃1
is_water: integer
Allowed: 0┃1
is_fat: integer
Allowed: 0┃1
energy: integer
energy_points: integer
energy_value: integer
fiber: number
fiber_points: integer
fiber_value: number
fruits_vegetables_nuts_colza_walnut_olive_oils: number
fruits_vegetables_nuts_colza_walnut_olive_oils_points: integer
fruits_vegetables_nuts_colza_walnut_olive_oils_value: number
proteins: number
proteins_points: integer
proteins_value: number
saturated_fat: number
saturated_fat_points: integer
saturated_fat_value: number
sodium: number
sodium_points: integer
sodium_value: number
sugars: number
sugars_points: integer
sugars_value: number
negative_points: integer
positive_points: integer
grade: enum

Nutri-Score for the product as a letter.

See https://world.openfoodfacts.org/nutriscore.
Allowed: a┃b┃c┃d┃e
score: integer
}
}
2023: {

Nutriscore2023
category_available: integer
Allowed: 0┃1
grade: enum

Nutri-Score for the product as a letter.

See https://world.openfoodfacts.org/nutriscore.
Allowed: a┃b┃c┃d┃e
nutrients_available: integer
Allowed: 0┃1
nutriscore_applicable: integer
Allowed: 0┃1
nutriscore_computed: integer
Allowed: 0┃1
score: integer
data: {

Nutriscore2023Data
is_beverage: integer
Allowed: 0┃1
is_cheese: integer
Allowed: 0┃1
is_water: integer
Allowed: 0┃1
is_fat_oil_nuts_seeds: integer
Allowed: 0┃1
is_red_meat_product: integer
Allowed: 0┃1
components: {...}

Nutriscore2023DataComponents
count_proteins: number
count_proteins_reason: string
negative_points: integer
positive_points: integer
negative_points_max: integer
positive_points_max: integer
positive_nutrients: [string]
}
}
}
nutriscore_2021_tags: [enum]

⮕ [ Nutri-Score for the product as a letter.

See https://world.openfoodfacts.org/nutriscore. ]
Allowed: a┃b┃c┃d┃e
nutriscore_2023_tags: [enum]

⮕ [ Nutri-Score for the product as a letter.

See https://world.openfoodfacts.org/nutriscore. ]
Allowed: a┃b┃c┃d┃e
nutriscore_data: {

Nutriscore2021Data: Detail of data the Nutri-Score was computed upon.

Note: this might not be stable, don't rely too much on this, or, at least, tell us !

TODO document each property
saturated_fat_ratio: number
saturated_fat_ratio_points: integer
saturated_fat_ratio_value: number
is_beverage: integer
Allowed: 0┃1
is_cheese: integer
Allowed: 0┃1
is_water: integer
Allowed: 0┃1
is_fat: integer
Allowed: 0┃1
energy: integer
energy_points: integer
energy_value: integer
fiber: number
fiber_points: integer
fiber_value: number
fruits_vegetables_nuts_colza_walnut_olive_oils: number
fruits_vegetables_nuts_colza_walnut_olive_oils_points: integer
fruits_vegetables_nuts_colza_walnut_olive_oils_value: number
proteins: number
proteins_points: integer
proteins_value: number
saturated_fat: number
saturated_fat_points: integer
saturated_fat_value: number
sodium: number
sodium_points: integer
sodium_value: number
sugars: number
sugars_points: integer
sugars_value: number
negative_points: integer
positive_points: integer
grade: enum

Nutri-Score for the product as a letter.

See https://world.openfoodfacts.org/nutriscore.
Allowed: a┃b┃c┃d┃e
score: integer
}
nutriscore_grade: enum

Nutri-Score for the product as a letter.

See https://world.openfoodfacts.org/nutriscore.
Allowed: a┃b┃c┃d┃e
nutriscore_score: integer

Nutri-Score for the product as an integer (see also nutriscore_grade).
nutriscore_score_opposite: integer
nutriscore_tags: [enum]

⮕ [ Nutri-Score for the product as a letter.

See https://world.openfoodfacts.org/nutriscore. ]
Allowed: a┃b┃c┃d┃e
nutriscore_version: string
data_quality_bugs_tags: [string]

⮕ [ Each data quality bug describes a data quality bug on the product.

Refer to the data quality taxonomy ]
data_quality_errors_tags: [string]

⮕ [ Each Data Quality Error describes a data quality error on the product.

Refer to the data quality taxonomy ]
data_quality_info_tags: [string]

⮕ [ Each Data Quality Info describes a data quality info on the product.

Refer to the data quality taxonomy ]
data_quality_tags: [string]

⮕ [ Each Data Quality tag describes a data quality issue on the product.

Refer to the data quality taxonomy ]
data_quality_warnings_tags: [string]

⮕ [ Each Data Quality warning tag describes a data quality warning on the product.

Refer to the data quality taxonomy ]
data_sources: string

Source of data imported from producers.
data_sources_tags: [string]
last_check_dates_tags: [string]
last_checked_t: integer
last_checker: string
states: string

comma separated list of values indicating some states of the product, like things to be done, or to be completed. See states taxonomy
states_hierarchy: [string]
states_tags: [string]

⮕ [ Each state describe something that is completed or is to be done or improved on the product.

Refer to states taxonomy ]
misc_tags: [string]

Information about different aspect of the product Refer to misc taxonomy
additives_original_tags: [string]
additives_prev_original_tags: [string]
added_countries_tags: [{
}]
allergens_from_ingredients: string
allergens_from_user: string
amino_acids_prev_tags: [{
}]
amino_acids_tags: [{
}]
carbon_footprint_percent_of_known_ingredients: integer
categories_properties: {
agribalyse_food_code:en: string
agribalyse_proxy_food_code:en: string
ciqual_food_code:en: string
}
categories_properties_tags: [string]
category_properties: {
[any-key]: string

those are properties taken from the category taxonomy
}
ciqual_food_name_tags: [string]
compared_to_category: string

the category to use for comparison.

TODO explain how it is chosen.
conservation_conditions: string
customer_service: string

Contact info of customer service.
expiration_date: string
link: string

link to the product on the website of the producer
main_countries_tags: [{
}]
minerals_prev_tags: [{
}]
minerals_tags: [{
}]
owner_fields: {

Those are fields provided by the producer (through producers platform), and the value he provided.
additionalProperties: {

you can retrieve all kind of properties, the same as on the parent object (the product). It's not processed entries (like tags for example) but raw ones.
ONE OF
1 integer
2 string
3 {
}
}
}
nova_groups_markers: {

Detail of ingredients or processing that makes the products having Nova 3 or 4
[any-key]: [[ string
]]
}
nucleotides_tags: [{
}]
origin: string
purchase_places: string

Country, state, or city where the product can be purchased.
purchase_places_tags: [string]
stores: string

Distributor name.
stores_tags: [string]
traces_from_ingredients: string
traces_from_user: string
created_t*: integer

Date when the product was added (UNIX timestamp format). See also entry_dates_tags
creator*: string

The contributor who added the product first.
editors_tags: [string]

List of editors who edited the product.
informers_tags: [string]
interface_version_created: string
interface_version_modified: string
languages: {
[pattern: en:(?<language_name>\w\w)]: integer

TODO explain !
}
languages_codes: {

Same as languages but by language code, instead of language tags
[pattern: (?<language_code>\w\w)]: integer
}
languages_hierarchy: [string]
languages_tags: [string]
last_edit_dates_tags: [string]
last_editor: string
last_modified_by*: string

The username of the user who last modified the product.
last_modified_t*: integer

Date when the product page was last modified. This date is updated only when primary data is modified (data entered by the user or updated by an interface)
last_updated_t*: integer

Date when the product page was last modified. This date is updated when primary data or secondary data is modified (primary: data entered by a user or read from an interface, secondary: data computed by a utility such as update_all_products.pl)
owner: string

Id of the producer in case he provides his own data about a product (producer platform).
owners_tags: string

Tagyfied version of owner
photographers_tags: [string]
rev*: integer

revision number of this product version (each edit adds a revision)
sources: [{
fields: [string]
id: string
images: [{
}]
import_t: integer
manufacturer: integer or string
name: string
source_licence: string
source_licence_url: string
url: null or string
}]
sources_fields: {

product_meta_source_fields
org-gs1: {
gln: string
gpcCategoryCode: string
gpcCategoryName: string
isAllergenRelevantDataProvided: string
lastChangeDateTime: string
partyName: string
productionVariantDescription: string
publicationDateTime: string
}
}
teams: string
teams_tags: [string]
update_key: string
knowledge_panels 🆁: {

panels: The panels object is a dictionary of individual panel objects. Each key of the dictionary is the id of the panel, and the value is the panel object.

Apps typically display a number of root panels with known panel ids (e.g. health_card and environment_card). Panels can reference other panels and display them as sub-panels.
[any-key]: {

panel: Each panel contains an optional title and an optional array of elements.
type: string

Type of the panel. If set to "card", the panel and its sub-panels should be displayed in a card. If set to "inline", the panel should have its content always displayed.
expanded: boolean

If true, the panel is to be displayed already expanded. If false, only the title should be displayed, and the user should be able to click or tap it to open the panel and display the elements.
expand_for: string

If set to "large", the content of the panel should be expanded on large screens, but it should still be possible to unexpand it.
evaluation: enum

An evaluation status specifically for this title element. This can be used to directly color the icon if 'icon_color_from_evaluation' is true and this field is present, or it might provide context for the title itself. e.g. bad is red. Please be careful in choosing colors, to avoid 50 shades of red.
Allowed: good┃average┃neutral┃bad┃unknown
half_width_on_mobile: boolean

If true, suggests that this panel could be rendered as half-width on mobile devices, allowing for side-by-side display with another half-width panel if applicable.
title_element: {

title_element: The title of a panel.
name: string

A short name of this panel, not including any actual values. e.g. "Fat"
title: string
subtitle: string
type: enum

Used to indicate how the value of this item is measured, such as "grade" for Nutri-Score and Green-Score or "percentage" for Salt
Allowed: grade┃percentage┃string
grade: enum

The value for this panel where it corresponds to a A to E grade such as the Nutri-Score or the Green-Score.
Allowed: a+┃a┃b┃c┃d┃e┃f┃unknown
value: number

The numeric value of the panel, where the type is "percentage"
value_string: string

The string value of the panel, for cases where the value is not numeric
icon_url: string
icon_color_from_evaluation: string
icon_size: string

If set to "small", the icon should be displayed at a small size.
}
elements: [{

An ordered list of elements to display in the content of the panel.
⮕ [ Each element object contains one specific element object such as a text element or an image element. ]
element_type*: enum

The type of the included element object. The element_type also indicates which field contains the included element object. e.g. if the element_type is "text", the included element object will be in the "text_element" field.

Note that in the future, new type of element may be added, so your code should ignore unrecognized types, and unknown properties.

TODO: add Map type
Allowed: text┃image┃action┃panel┃panel_group┃table
text_element: {...}

text_element: A text in simple HTML format to display.

For some specific texts that correspond to a product field (e.g. a product name, the ingredients list of a product),the edit_field_* fields are used to indicate how to edit the field value.
image_element: {...}

image_element
action_element: {...}

action_element: The action element is used to display a title and a list of actions like editing a product or adding categories.
panel_element: {...}

panel_element: Panels can include other panels as sub-panels using the panel_element.
panel_group_element: {...}

panel_group_element: The panel group element is used to display an optional title followed by a number of sub-panels.
table_element: {...}

table_element: Element to display a table.
}]
level: string

a message level, as levels we use in log. It might help theming the panel visually. Some possible values: info, recommendation
size: enum

size is either empty (normal display) or small to indicate a panel that should have a smaller font size
Allowed: small
topics: [string]

topics currently include health, environment, problem
}
}
attribute_groups: [{

Each element is an attribute that can help compute a personal ranking for the product
id: string

Unique id of the attribute.

It will be use to match against preferences parameters.
status: enum

wether we have the information to really compute this criteria or not.
Allowed: known┃unknown
title: string

A descriptive sentence about the situation of the product concerning attribute
match: number

a numeric value for the match, telling how much the products ranks well for this particular attribute. The higher the value, the better the match.
Constraints: Min 0┃Max 100
grade: enum

every attribute as a grade for a to e
Allowed: unknown┃a┃b┃c┃d┃e
name: string

The name of attribute, for eventual display
icon_url: string

an icon representing the attribute match (often using a color)
description: string

An eventual description of the value of the property upon which this attribute is based
description_short: string

An eventual short description of the value of the property upon which this attribute is based
}]
[pattern: product_name_(?<language_code>\w\w)]: string

Product name in language designated by language_code, e.g. product_name_fr
[pattern: abbreviated_product_name_(?<language_code>\w\w)]: string

Abbreviated product name in language language_code.
[pattern: generic_name_(?<language_code>\w\w)]: string

Generic/legal name in language designated by language_code, e.g. generic_name_en.
[pattern: food_groups_(?<language_code>\w\w)]: string

see food_groups
[pattern: packaging_text_(?<language_code>\w\w)]: string

Packaging text in language designated by language_code
[pattern: image(_(?<image_type>front|packaging|ingredients|nutrition|other))?(_(?<image_size>small|thumb))?_url]: string

the URL of image of type image_type in size image_size (or full size if not given).

The image_type tells which image the url correspond to. image_type is front if not provided.

The image is the one for current language (affected by lc parameter) if an image exists for this language, the image in main product language otherwise.

IMPORTANT: you should use selected_images field instead of this one.
[pattern: ingredients_text_(?<language_code>\w\w)]: string

Raw list of ingredients in language given by 'language_code'.

See ingredients_text
[pattern: ingredients_text_with_allergens_(?<language_code>\w\w)]: string

Like ingredients_text_with_allergens for a particular language
[pattern: conservation_conditions_(?<language_code>\w\w)]: string
[pattern: customer_service_(?<language_code>\w\w)]: string
[pattern: origin_(?<language_code>\w\w)]: string

origin in language indicated by language_code
}]
skip: integer

Number of matching products skipped before the current page.
} 