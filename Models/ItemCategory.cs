using System;

namespace negosuite_api.Models
{
    public class ItemCategory
    {
        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Name { get; set; }
        public bool Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
    }
}
