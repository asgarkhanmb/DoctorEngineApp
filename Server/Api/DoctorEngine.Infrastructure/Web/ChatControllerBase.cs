using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Localization;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Web
{
    /// <summary>
    /// Çatın ortaq endpoint-ləri (api/chat/*) — sayt (DoctorEngineApp/ChatApiController) və API (DoctorEngine.Api/ChatController)
    /// eyni məntiqi istifadə edir; marşrut prefiksi törəmə sinifdədir.
    ///  - şəxsi yazışma: müştəri ↔ ustalar; qrup: "g:{id}"; yayım: hər müştəriyə ayrıca
    /// Canlı hadisələr: /chatHub.
    /// </summary>
    [Authorize]
    public abstract class ChatControllerBase : ControllerBase
    {
        // Maksimum video + form üçün kiçik ehtiyat
        protected const long MaxRequestBytes = ChatService.MaxVideoBytes + 1024 * 1024;

        protected readonly IChatService Service;
        protected readonly IChatNotifier Notifier;
        protected readonly PushService Push;
        private readonly IServiceScopeFactory _scopeFactory;

        protected ChatControllerBase(IChatService service, IChatNotifier notifier, PushService push, IServiceScopeFactory scopeFactory)
        {
            Service = service;
            Notifier = notifier;
            Push = push;
            _scopeFactory = scopeFactory;
        }

        protected string Me => User.Identity?.Name ?? "";
        protected bool IsAdmin => User.IsInRole(nameof(Roles.Admin));

        private Task<string?> ConversationAsync(string? conversation) =>
            Service.ResolveConversationAsync(Me, IsAdmin, conversation);

        private ObjectResult NoConversation() =>
            NotFound(new { error = L.T("Yazışma tapılmadı.") });

        // ================= OXU =================

        /// <summary>Yazışmanın mesajları (köhnədən yeniyə) + qarşı tərəf / qrup.</summary>
        [HttpGet("messages")]
        public async Task<ActionResult<ChatPageDto>> Messages([FromQuery] string? conversation, [FromQuery] int? beforeId, [FromQuery] int take = 30)
        {
            var conv = await ConversationAsync(conversation);
            if (conv == null) return NoConversation();
            return await Service.GetPageAsync(conv, Me, IsAdmin, beforeId, take);
        }

        /// <summary>"Çatlar" siyahısı: usta — bütün yazışmalar və qruplar; müştəri — ustalarla yazışması və qrupları.</summary>
        [HttpGet("conversations")]
        public Task<List<ChatConversationDto>> Conversations() => Service.GetConversationsAsync(Me, IsAdmin);

        [HttpGet("search")]
        public async Task<ActionResult<List<ChatMessageDto>>> Search([FromQuery] string? conversation, [FromQuery] string q)
        {
            var conv = await ConversationAsync(conversation);
            if (conv == null) return NoConversation();
            return await Service.SearchAsync(conv, Me, q);
        }

        [HttpGet("unread")]
        public async Task<IActionResult> Unread() =>
            Ok(new { count = await Service.GetUnreadCountAsync(Me, IsAdmin) });

        // ================= ✓✓ =================

        [HttpPost("read")]
        public Task<IActionResult> Read([FromForm] int lastId, [FromForm] string? conversation) =>
            ReceiptAsync(conversation, lastId, read: true);

        [HttpPost("delivered")]
        public Task<IActionResult> Delivered([FromForm] int lastId, [FromForm] string? conversation) =>
            ReceiptAsync(conversation, lastId, read: false);

        private async Task<IActionResult> ReceiptAsync(string? conversation, int lastId, bool read)
        {
            var conv = await ConversationAsync(conversation);
            if (conv == null) return NoConversation();

            var receipt = read
                ? await Service.MarkReadAsync(conv, Me, IsAdmin, lastId)
                : await Service.MarkDeliveredAsync(conv, Me, IsAdmin, lastId);
            if (receipt != null)
                await Notifier.ReceiptAsync(receipt);
            return Ok();
        }

        // ================= YAZ =================

        [HttpPost("send")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Send([FromForm] ChatSendVM vm)
        {
            var conv = await ConversationAsync(vm.Conversation);
            if (conv == null) return NoConversation();

            var result = await Service.SendAsync(vm, conv, Me, IsAdmin);
            if (result.Status != ChatResultStatus.Ok)
                return BadRequest(new { error = result.Error });

            await Notifier.MessageCreatedAsync(result.Message!);
            QueuePush(new[] { result.Message! });
            return Ok(result.Message);
        }

        [HttpPost("edit")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Edit([FromForm] ChatEditVM vm)
        {
            var result = await Service.EditAsync(vm, Me);
            if (result.Status == ChatResultStatus.NotFound)
                return NotFound(new { error = L.T("Mesaj tapılmadı.") });
            if (result.Status == ChatResultStatus.Invalid)
                return BadRequest(new { error = result.Error });

            await Notifier.MessageChangedAsync(result.Message!);
            return Ok(result.Message);
        }

        [HttpPost("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var conv = await Service.DeleteAsync(id, Me, IsAdmin);
            if (conv == null)
                return NotFound(new { error = L.T("Mesaj tapılmadı.") });

            await Notifier.MessageDeletedAsync(conv, id);
            return Ok();
        }

        /// <summary>Reaksiya: eyni emoji təkrar göndərilsə və ya emoji boşdursa — götürülür.</summary>
        [HttpPost("react")]
        public async Task<IActionResult> React([FromForm] int id, [FromForm] string? emoji)
        {
            var msg = await Service.ReactAsync(id, emoji, Me, IsAdmin);
            if (msg == null)
                return NotFound(new { error = L.T("Mesaj tapılmadı.") });

            await Notifier.MessageChangedAsync(msg);
            return Ok(msg);
        }

        // ================= YAZIŞMANI SİL =================

        /// <summary>
        /// Yazışmanı sil: forEveryone = false — yalnız mənim üçün (yeni mesaj gələndə yazışma yenidən görünür);
        /// forEveryone = true — yalnız usta: bütün mesajlar və fayllar hamı üçün silinir.
        /// </summary>
        [HttpPost("clear")]
        public async Task<IActionResult> Clear([FromForm] string? conversation, [FromForm] bool forEveryone = false)
        {
            var conv = await ConversationAsync(conversation);
            if (conv == null) return NoConversation();
            var (ok, error) = await Service.ClearConversationAsync(conv, Me, IsAdmin, forEveryone);
            if (!ok) return BadRequest(new { error });
            await Notifier.ConversationClearedAsync(conv, Me, forEveryone);
            return Ok();
        }

        // ================= QRUPLAR =================

        /// <summary>Ustalar: istifadəçilər (qrupa üzv seçmək üçün).</summary>
        [HttpGet("users")]
        [Authorize(Roles = nameof(Roles.Admin))]
        public Task<List<ChatUserDto>> Users([FromQuery] string? q) => Service.GetUsersAsync(q);

        /// <summary>Yeni çat ("+"): yaza biləcəyim istifadəçilər (hər kəs; email göstərilmir).</summary>
        [HttpGet("people")]
        public Task<List<ChatUserDto>> People([FromQuery] string? q) => Service.GetPeopleAsync(Me, q);

        /// <summary>İstifadəçiyə yaz: yazışmanın açarını qaytarır (lazım olsa şəxsi yazışma yaradılır).</summary>
        [HttpPost("direct")]
        public async Task<IActionResult> Direct([FromForm] string? userName)
        {
            var (conversation, error) = await Service.StartDirectAsync(Me, IsAdmin, userName ?? "");
            if (conversation == null) return BadRequest(new { error });
            return Ok(new { conversation });
        }

        [HttpGet("groups/{id:int}")]
        public async Task<ActionResult<ChatGroupInfoDto>> Group(int id)
        {
            var g = await Service.GetGroupAsync(id, Me, IsAdmin);
            return g == null ? NoConversation() : g;
        }

        [HttpPost("groups")]
        [Authorize(Roles = nameof(Roles.Admin))]
        public async Task<IActionResult> CreateGroup([FromForm] ChatGroupSaveVM vm)
        {
            var (group, error) = await Service.CreateGroupAsync(vm, Me);
            if (group == null) return BadRequest(new { error });
            await Notifier.GroupChangedAsync(group.Conversation);
            return Ok(group);
        }

        [HttpPost("groups/{id:int}")]
        [Authorize(Roles = nameof(Roles.Admin))]
        public async Task<IActionResult> UpdateGroup(int id, [FromForm] ChatGroupSaveVM vm)
        {
            var (group, error) = await Service.UpdateGroupAsync(id, vm);
            if (group == null) return BadRequest(new { error });
            await Notifier.GroupChangedAsync(group.Conversation);
            return Ok(group);
        }

        [HttpPost("groups/{id:int}/remove")]
        [Authorize(Roles = nameof(Roles.Admin))]
        public async Task<IActionResult> RemoveMember(int id, [FromForm] string userName)
        {
            var group = await Service.RemoveMemberAsync(id, userName);
            if (group == null) return NoConversation();
            // Çıxarılan üzvün siyahısından da itsin
            await Notifier.GroupRemovedAsync(group.Conversation, new ChatAudience(true, false, new[] { userName }));
            await Notifier.GroupChangedAsync(group.Conversation);
            return Ok(group);
        }

        [HttpPost("groups/{id:int}/delete")]
        [Authorize(Roles = nameof(Roles.Admin))]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            var audience = await Service.DeleteGroupAsync(id);
            if (audience == null) return NoConversation();
            await Notifier.GroupRemovedAsync("g:" + id, audience);
            return Ok();
        }

        // ================= PROFİL VƏ MEDİA =================

        /// <summary>Profil dəyişəndən sonra (sayt: cookie-dəki ad / şəkil yenilənsin).</summary>
        protected virtual Task OnProfileChangedAsync() => Task.CompletedTask;

        [HttpGet("profile")]
        public async Task<ActionResult<ChatProfileDto>> Profile()
        {
            var p = await Service.GetProfileAsync(Me);
            return p == null ? NotFound() : p;
        }

        [HttpPost("profile")]
        public async Task<IActionResult> SaveProfile([FromForm] ChatProfileSaveVM vm)
        {
            var (profile, error) = await Service.UpdateProfileAsync(Me, vm);
            if (profile == null) return BadRequest(new { error });
            await OnProfileChangedAsync();
            return Ok(profile);
        }

        [HttpPost("profile/avatar")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile? file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { error = L.T("Şəkil seçin.") });
            var (profile, error) = await Service.SetAvatarAsync(Me, file);
            if (profile == null) return BadRequest(new { error });
            await OnProfileChangedAsync();
            return Ok(profile);
        }

        [HttpPost("profile/avatar/delete")]
        public async Task<IActionResult> RemoveAvatar()
        {
            var (profile, error) = await Service.SetAvatarAsync(Me, null);
            if (profile == null) return BadRequest(new { error });
            await OnProfileChangedAsync();
            return Ok(profile);
        }

        /// <summary>Başlığa toxunanda: əlaqənin / qrupun məlumatı.</summary>
        [HttpGet("contact")]
        public async Task<ActionResult<ChatContactDto>> Contact([FromQuery] string? conversation)
        {
            var conv = await ConversationAsync(conversation);
            if (conv == null) return NoConversation();
            var c = await Service.GetContactAsync(conv, Me, IsAdmin);
            return c == null ? NoConversation() : c;
        }

        /// <summary>Yazışmanın mediası: kind = media (şəkil/video) | audio | links.</summary>
        [HttpGet("media")]
        public async Task<ActionResult<ChatPageDto>> Media([FromQuery] string? conversation, [FromQuery] string kind = "media",
                                                           [FromQuery] int? beforeId = null, [FromQuery] int take = 60)
        {
            var conv = await ConversationAsync(conversation);
            if (conv == null) return NoConversation();
            return await Service.GetMediaAsync(conv, Me, kind, beforeId, take);
        }

        // ================= YAYIM =================

        /// <summary>Ustalar: mesaj hər müştərinin şəxsi yazışmasına ayrıca göndərilir.</summary>
        [HttpPost("broadcast")]
        [Authorize(Roles = nameof(Roles.Admin))]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Broadcast([FromForm] ChatBroadcastVM vm)
        {
            var (messages, error) = await Service.BroadcastAsync(vm, Me);
            if (error != null) return BadRequest(new { error });

            foreach (var m in messages)
                await Notifier.MessageCreatedAsync(m);
            QueuePush(messages);
            return Ok(new { count = messages.Count });
        }

        /// <summary>Bildirişlər cavabı gecikdirməsin — arxa planda, ayrıca scope ilə göndərilir.</summary>
        private void QueuePush(IReadOnlyCollection<ChatMessageDto> messages)
        {
            if (!Push.IsConfigured || messages.Count == 0) return;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var push = scope.ServiceProvider.GetRequiredService<PushService>();
                foreach (var message in messages)
                {
                    try { await push.NotifyNewMessageAsync(message); }
                    catch (Exception ex)
                    {
                        scope.ServiceProvider.GetRequiredService<ILogger<ChatControllerBase>>()
                            .LogWarning(ex, "Push bildirişləri göndərilmədi");
                    }
                }
            });
        }
    }
}
