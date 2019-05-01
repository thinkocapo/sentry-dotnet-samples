using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sentry.Samples.AspNetCore.Mvc.Models;
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Sentry;
using Microsoft.AspNetCore.Http.Internal;
using Sentry.Samples.AspNetCore.Mvc;


public class User
{
    public string Email { get; set; }
}

public static class Store
{
    // *TODO make a JObject
    // public static string inventory = @"{
    //             wrench: '1',
    //             nails: '1',
    //             hammer: '1'
    //             }";
    public static JObject inventory = new JObject
    {
        { "wrench", 1 },
        { "nails", 1 },
        { "hammer", 1 }
    };
}

// TODO - MIDDLEWARE
// var body = reader.ReadToEnd();
// _logger.LogInformation(body);
// reader.Close();
// if (body != "") then set the email       

namespace Sentry.Samples.AspNetCore.Mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly IGameService _gameService;
        private readonly ILogger<HomeController> _logger;
        public HomeController(IGameService gameService, ILogger<HomeController> logger)
        {
            _gameService = gameService;
            _logger = logger;
            _logger.LogInformation("\n\n ************ HOME CONTROLLER ************* \n");
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<String> checkout()
        {
            using (var reader = new StreamReader(Request.Body))
            {
                var body = reader.ReadToEnd();
                reader.Close();
                JObject order = JObject.Parse(body);
                JToken cart = order["cart"];

                // MIDDLEWARE - User, Tags (transaction_id, session_id), Headers
                String email = order["email"].ToString();
                String transaction_id = Request.Headers["X-transaction-ID"];
                String session_id = Request.Headers["X-session-ID"];
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.User = new Sentry.Protocol.User
                    {
                        Email = email
                    };
                });
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("transaction_id", transaction_id);
                });
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("session_id", session_id);
                });
                SentrySdk.ConfigureScope(scope => 
                {
                    scope.SetExtra("inventory", Store.inventory);
                });

                _logger.LogInformation("\n*********** BEFORE " + Store.inventory.ToString());

                JObject tempInventory = Store.inventory;
                foreach (var item in cart)
                {
                    if (Int32.Parse(Store.inventory[item["id"].ToString()].ToString()) <= 0)
                    {
                        _logger.LogInformation("\nNot enough inventory for " + item["id"].ToString());
                        throw null;
                    }
                    else
                    {
                        tempInventory[item["id"].ToString()] = (Int32.Parse(tempInventory[item["id"].ToString()].ToString()) - 1).ToString();
                    }
                }
                Store.inventory = tempInventory;

                _logger.LogInformation("\n*********** AFTER " + Store.inventory.ToString());
                return "SUCCESS: checkout";
            }


        }


        [HttpGet]
        public async Task<String> handled([FromServices] IHub sentry)
        {
            try
            {
                // TODO - consider math exception as its a more readable error on Sentry ("Can't divide by 0" vs "NullExceptionValue")
                // int n1 = 1;
                // int n2 = 0;
                // int ans = n1 / n2;
                throw null; // System.NullReferenceException - Object reference not set to an instance of an object.
            }
            catch (Exception exception)
            {
                exception.Data.Add("detail",
                    new
                    {
                        Reason = "There's a 'throw null' hard-coded in the try block"
                    });

                var id = sentry.CaptureException(exception); // ViewData["Message"] = "An exception was caught and sent to Sentry! Event id: " + id;
            }
            return "SUCCESS: back-end error handled gracefully";
        }


        [HttpGet]
        public async Task<String> unhandled()
        {
            int n1 = 1;
            int n2 = 0;
            int ans = n1 / n2;
            return "FAILURE: Server-side Error";
        }


        // Example: An exception that goes unhandled by the app will be captured by Sentry:
        [HttpPost]
        public async Task PostIndex(string @params)
        {
            try
            {
                if (@params == null)
                {
                    _logger.LogWarning("Param is null!", @params);
                }

                await _gameService.FetchNextPhaseDataAsync();
            }
            catch (Exception e)
            {
                var ioe = new InvalidOperationException("Bad POST! See Inner exception for details.", e);

                ioe.Data.Add("inventory",
                    // The following anonymous object gets serialized:
                    new
                    {
                        SmallPotion = 3,
                        BigPotion = 0,
                        CheeseWheels = 512
                    });

                throw ioe;
            }
        }

        // Example: An exception that goes unhandled by the app will be captured by Sentry:
        [HttpPost]
        public async Task PostIndexUnhandled(string @params)
        {
            if (@params == null)
            {
                _logger.LogWarning("Param is null!", @params);
            }
            await _gameService.FetchNextPhaseDataAsync();
        }

        // Example: The view rendering throws: see about.cshtml
        public IActionResult About()
        {
            return View();
        }

        // Example: To take the Sentry Hub and submit errors directly:
        public IActionResult Contact(
            // Hub holds a Client and Scope management
            // Errors sent with the hub will include all context collected in the current scope
            [FromServices] IHub sentry)
        {
            try
            {
                // Some code block that could throw
                throw null;
            }
            catch (Exception e)
            {
                e.Data.Add("detail",
                    new
                    {
                        Reason = "There's a 'throw null' hard-coded here!",
                        IsCrazy = true
                    });

                var id = sentry.CaptureException(e);

                ViewData["Message"] = "An exception was caught and sent to Sentry! Event id: " + id;
            }
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
