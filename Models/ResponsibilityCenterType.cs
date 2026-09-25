using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public partial class ResponsibilityCenterType
    {
        public ResponsibilityCenterType()
        {
        }

        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public string RequiredBy { get; set; }
        public string RequiredByTags { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        [NotMapped]
        public bool? Deleted { get; set; }

    }
}
