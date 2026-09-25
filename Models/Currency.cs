using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Currency
    {
        public Currency()
        {
        }

        public short Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal ExchangeRate { get; set; }
        public bool IsBase { get; set; }
        public string AltCode { get; set; }
    }
}
