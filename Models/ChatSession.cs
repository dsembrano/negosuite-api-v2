using System;
using System.Collections.Generic;

namespace negosuite_api.Models
{
    public partial class ChatSession
    {
        public ChatSession()
        {
            ChatMessages = new HashSet<ChatMessage>();
        }

        public string Id { get; set; }
        public int UserId { get; set; }
        public string SessionName { get; set; }
        public short Status { get; set; }
        public string Metadata { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public virtual ICollection<ChatMessage> ChatMessages { get; set; }
    }
}

public enum ChatStatus
{
    Active, 
    Archived, 
    Deleted
}