using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Controllers
{
    //[Authorize]
    //[TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/chats")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly negosuiteContext _context;
        public static short STATUS_DELETED = -1;

        public ChatController(negosuiteContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult> GetChatSessions(int userId)
        {
            var result = await _context.ChatSessions
                .Where(e => e.UserId == userId && e.Status != STATUS_DELETED)
                .Take(100).OrderByDescending(e => e.CreatedAt).ToListAsync();
            return Ok(result);
        }

        [HttpGet("messages")]
        public async Task<ActionResult> GetChatMessages(string chatSessionId)
        {
            var result = await _context.ChatMessages
                .Where(e => e.ChatSessionId == chatSessionId).OrderBy(e => e.SentAt).ToListAsync();
            return Ok(result);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutChat(string id, ChatSession chatSession)
        {
            if (id != chatSession.Id)
            {
                return BadRequest();
            }

            _context.Entry(chatSession).State = EntityState.Modified;

            try
            {
                chatSession.LastActivityAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ChatSessionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteChatSession(string id)
        {
            var chatSession = await _context.ChatSessions
                .Where(e => e.Id == id).Include(e => e.ChatMessages).FirstOrDefaultAsync();
                //.FindAsync(id);
            if (chatSession == null)
            {
                return NotFound();
            }

            //_context.ChatSessions.Remove(chatSession);

            _context.Entry(chatSession).State = EntityState.Modified;
           
            try
            {
                chatSession.LastActivityAt = DateTime.UtcNow;
                chatSession.Status = STATUS_DELETED;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ChatSessionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        private bool ChatSessionExists(string id)
        {
            return _context.ChatSessions.Any(e => e.Id == id);
        }

    }
}
