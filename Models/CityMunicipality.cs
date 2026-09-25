using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class CityMunicipality
    {
        public CityMunicipality()
        {
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public int StateProvinceId { get; set; }
        public string PostalCode { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual StateProvince StateProvince { get; set; }
    }
}
