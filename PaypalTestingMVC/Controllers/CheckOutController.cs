using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace PaypalTestingMVC.Controllers
{
    public class CheckOutController : Controller
    {
        private string PayPalClientId { get; set; } = "";
        private string PaypalSecret { get; set; } = "";
        private string PaypalUrl { get; set; } = "";

        public CheckOutController(IConfiguration config)
        {
            PayPalClientId = config["payPalSettings:ClientId"]!;
            PaypalSecret = config["payPalSettings:Secret"]!;
            PaypalUrl = config["payPalSettings:Url"]!;
        }


        public IActionResult Index()
        {
            ViewBag.PaypalClientId = PayPalClientId;
            return View();
        }


        [HttpPost]
        public async Task<JsonResult> CreateOrder([FromBody] JsonObject data)
        {
            // Gets the amount from Index.cshtml from js script
            // https://developer.paypal.com/docs/api/orders/v2/#orders_create
            var totalAmount = data?["amount"]?.ToString();
            if (totalAmount == null)
            {
                return new JsonResult(new {Id = 0});
            }

            // Create the request body
            JsonObject createOrderRequest = new JsonObject();
            createOrderRequest.Add("intent", "CAPTURE");

            JsonObject amount = new JsonObject();
            amount.Add("currency_code", "USD");
            amount.Add("value", totalAmount);

            JsonObject purchaseUnit1 = new JsonObject();
            purchaseUnit1.Add("amount", amount);

            JsonArray purchaseUnits = new JsonArray();
            purchaseUnits.Add(purchaseUnit1);

            createOrderRequest.Add("purchase_units", purchaseUnits);


            // get access token
            string accessToken = await GetPayPalAccessToken();

            // send request
            string url = PaypalUrl + "/v2/checkout/orders";

            // Creating httpClient
            using (var client = new HttpClient()) 
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = new StringContent(createOrderRequest.ToString(), null, "application/json");

                var  httpResponse = await client.SendAsync(requestMessage);


                if (httpResponse.IsSuccessStatusCode)
                {
                    var strResponse = await httpResponse.Content.ReadAsStringAsync();
                    var jsonResponse = JsonNode.Parse(strResponse);

                    if (jsonResponse != null)
                    {
                        string paypalOrderId = jsonResponse["id"]?.ToString() ?? "";

                        return new JsonResult(new {Id = paypalOrderId});
                    }
                }
            }


            return new JsonResult(new {Id = 0});
        }

        [HttpPost]
        public async Task<JsonResult> CompleteOrder([FromBody] JsonObject data)
        {
            var orderId = data["orderId"]?.ToString();
            if(orderId == null)
            {
                return new JsonResult("error");
            }

            // get access token
            string accessToken = await GetPayPalAccessToken();

            // https://developer.paypal.com/docs/api/orders/v2/#orders_capture
            string url = PaypalUrl + "/v2/checkout/orders/"+ orderId+"/capture";

            using (var client = new HttpClient()) 
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = new StringContent("", null, "application/json");

                var httpResponse = await client.SendAsync(requestMessage);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var strResponse = await httpResponse.Content.ReadAsStringAsync();
                    var jsonResponse = JsonNode.Parse(strResponse);

                    if(jsonResponse != null)
                    {
                        string paypalOrderStatus = jsonResponse["status"]?.ToString() ?? "";
                        if(paypalOrderStatus == "COMPLETED")
                        {
                            // Save the order in the database

                            return new JsonResult("success");
                        }
                    }
                }

            }

            return new JsonResult("error");
        }


        //public async Task<string> TokenTest()
        //{
        //    return await GetPayPalAccessToken();
        //}

        private async Task<string> GetPayPalAccessToken()
        {
            // Link for REST API authentication of Paypal
            // https://developer.paypal.com/api/rest/authentication/

            string accessToken = "";

            // send request to paypal server - application that contains a Buissness(seller)
            string url = PaypalUrl + "/v1/oauth2/token";

            using (var client = new HttpClient())
            {
                string credentials64 =
                    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(PayPalClientId + ":" + PaypalSecret));

                client.DefaultRequestHeaders.Add("Authorization", "Basic " + credentials64);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = new StringContent("grant_type=client_credentials", null
                    , "application/x-www-form-urlencoded");

                var httpResponse = await client.SendAsync(requestMessage);


                if (httpResponse.IsSuccessStatusCode)
                {
                    var strResponse = await httpResponse.Content.ReadAsStringAsync();

                    var jsonResponse = JsonNode.Parse(strResponse);
                    if (jsonResponse != null)
                    {
                        // read accessToken from the response
                        accessToken = jsonResponse["access_token"]?.ToString() ?? "";
                    }


                }

                return accessToken;
            }


        }

    }
}
