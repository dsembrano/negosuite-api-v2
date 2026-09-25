using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Vcitymunicipality
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int StateProvinceId { get; set; }
        public string StateProvinceName { get; set; }
        public int CountryId { get; set; }
        public string CountryCode { get; set; }
        public string CountryName { get; set; }
    }
}
