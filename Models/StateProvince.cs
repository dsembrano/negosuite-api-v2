using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class StateProvince
    {
        public StateProvince()
        {
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Capital { get; set; }
        public int CountryId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual Country Country { get; set; }
    }
}
