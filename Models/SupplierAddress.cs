using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public partial class SupplierAddress
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public int CityMunicipalityId { get; set; }
        public string PostalCode { get; set; }
        public bool IsPrimaryAddress { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        public virtual CityMunicipality CityMunicipality { get; set; }
        [NotMapped]
        public bool? Deleted { get; set; }
    }
}
