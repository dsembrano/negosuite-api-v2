using System;

namespace negosuite_api.Models
{
    public class SubscriptionPlan
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public decimal PriceBilledMonthly { get; set; }
        public decimal PriceBilledYearly { get; set; }
        public short MinimumUsers { get; set; }
        public decimal? PricePerAdditionalUser { get; set; }
        public string Data { get; set; }
        public bool IsActive { get; set; }        
    }
}


