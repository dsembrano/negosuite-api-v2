using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public partial class AccountCategory
    {
        public AccountCategory()
        {
        }

        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int OrderNo { get; set; }
        public string AccountCodePrefix { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
    }

}
