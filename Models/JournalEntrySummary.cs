namespace negosuite_api.Models
{
    public class JournalEntrySummary
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
