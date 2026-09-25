namespace negosuite_api.Models
{
    public class TransactionSequence
    {
        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Source { get; set; }
        public int LastSequence { get; set; }
    }
}
