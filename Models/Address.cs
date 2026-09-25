using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Address
    {
        public Address()
        {
        }

        public int Id { get; set; }
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public int CityMunicipalityId { get; set; }
        public string PostalCode { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual CityMunicipality CityMunicipality { get; set; }
    }
}
