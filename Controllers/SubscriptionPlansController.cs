using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Controllers
{
    [Authorize]
    [Route("api/subscription-plans")]
    [ApiController]
    public class SubscriptionPlansController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public SubscriptionPlansController(negosuiteContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult> GetSubscriptionPlans()
        {

            var result = await _context.SubscriptionPlans.Where(s => s.IsActive == true)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Notes,
                    s.PriceBilledMonthly,
                    s.PriceBilledYearly,
                    s.MinimumUsers,
                    s.PricePerAdditionalUser
                }).ToListAsync();

            return Ok(result);
        }


        private bool SubscriptionPlansExists(int id)
        {
            return _context.SubscriptionPlans.Any(e => e.Id == id);
        }

    }

}
