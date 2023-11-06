using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using WebAPI.DataModels;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [ApiController]
    //[ApiVersion("1.0")]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {

        private readonly PaymentService _paymentService;

        public PaymentController(PaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost]
        [Route("purchase-subscription")]
        public ActionResult CreateSubscription([FromBody] PaymentCard pc)
        {
           if (_paymentService.CreateSubscription(pc))
            {
                return Ok();
            }
           else { return BadRequest(); }

             
        }
    }
}
