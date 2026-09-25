
using System;

namespace negosuite_api.Models
{
    public partial class ChatMessage
    {
        public ChatMessage()
        {
        }

        public string Id { get; set; }
        public string ChatSessionId { get; set; }
        public string MessageType { get; set; }
        public string Content { get; set; }
        public bool? IsJsonContent { get; set; }
        public DateTime? SentAt { get; set; }
        public int TokensUsed { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public string ModelUsed { get; set; }
        public bool? IsContextMessage { get; set; }
        public string ParentMessageId { get; set; }
        public string Metadata { get; set; }
        public string Data { get; set; }
    }
}


public enum MessageType
{
    User,
    AI,
    System
}