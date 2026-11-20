using DoctorEngineApp.Hubs;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Chats;
using Microsoft.AspNetCore.SignalR;

namespace DoctorEngineApp.Services
{
    /// <summary>
    /// SignalR ilə çat hadisələri — yalnız yazışmanın iştirakçılarına:
    ///  - şəxsi yazışma: həmin müştəri + ustalar
    ///  - qrup: üzvlər + ustalar ("bütün istifadəçilər" qrupu — hamı)
    /// Siyahı üçün: ustalara ConversationUpdated (şəxsi), hamıya ConversationChanged (klient siyahını yeniləyir).
    /// </summary>
    public class ChatNotifier : IChatNotifier
    {
        private readonly IHubContext<ChatHub> _hub;
        private readonly IChatService _chat;
        private readonly ILogger<ChatNotifier> _logger;

        public ChatNotifier(IHubContext<ChatHub> hub, IChatService chat, ILogger<ChatNotifier> logger)
        {
            _hub = hub;
            _chat = chat;
            _logger = logger;
        }

        public static IClientProxy Target(IHubClients<IClientProxy> clients, ChatAudience audience)
        {
            if (audience.Everyone) return clients.All;
            // Şəxsi yazışma: yalnız iki üzvün öz cihazları (usta da olsa — "chat-self-…" qrupu)
            if (audience.Private) return clients.Groups(audience.UserNames.Select(ChatHub.SelfGroup).Distinct().ToList());
            var groups = audience.UserNames.Select(ChatHub.UserGroup).Append(ChatHub.AdminsGroup).Distinct().ToList();
            return clients.Groups(groups);
        }

        private async Task<IClientProxy> TargetAsync(string conversation) =>
            Target(_hub.Clients, await _chat.GetAudienceAsync(conversation));

        public async Task MessageCreatedAsync(ChatMessageDto message)
        {
            var target = await TargetAsync(message.Conversation);
            await target.SendAsync("ReceiveMessage", message);
            await ConversationChangedAsync(message.Conversation, target);
        }

        public async Task MessageChangedAsync(ChatMessageDto message) =>
            await (await TargetAsync(message.Conversation)).SendAsync("EditMessage", message);

        public async Task MessageDeletedAsync(string conversation, int id)
        {
            var target = await TargetAsync(conversation);
            await target.SendAsync("DeleteMessage", id);
            await ConversationChangedAsync(conversation, target);
        }

        public async Task ReceiptAsync(ChatReceiptDto receipt)
        {
            var target = await TargetAsync(receipt.Conversation);
            await target.SendAsync("Receipt", receipt);
            await ConversationChangedAsync(receipt.Conversation, target);
        }

        public async Task ConversationClearedAsync(string conversation, string byUser, bool forEveryone)
        {
            // Mənim üçün — yalnız öz cihazlarım; hamı üçün — yazışmanın bütün iştirakçıları
            var target = forEveryone ? await TargetAsync(conversation) : _hub.Clients.Group(ChatHub.SelfGroup(byUser));
            await target.SendAsync("ConversationCleared", new { conversation, forEveryone });
            await target.SendAsync("ConversationChanged", conversation);
        }

        public async Task GroupChangedAsync(string conversation) =>
            await ConversationChangedAsync(conversation, await TargetAsync(conversation));

        public async Task GroupRemovedAsync(string conversation, ChatAudience audience) =>
            await Target(_hub.Clients, audience).SendAsync("ConversationRemoved", conversation);

        private async Task ConversationChangedAsync(string conversation, IClientProxy target)
        {
            try
            {
                // Hər istifadəçinin sətri fərqlidir (oxunmamış sayı) — klient siyahını özü yeniləyir
                await target.SendAsync("ConversationChanged", conversation);

                // Ustalar üçün şəxsi yazışmanın hazır sətri (köhnə klientlərlə uyğunluq)
                var row = await _chat.GetConversationAsync(conversation);
                if (row != null)
                    await _hub.Clients.Group(ChatHub.AdminsGroup).SendAsync("ConversationUpdated", row);
            }
            catch (Exception ex)
            {
                // Siyahının yenilənməməsi mesajın özünə mane olmamalıdır
                _logger.LogWarning(ex, "Çat siyahısı yenilənmədi: {Conversation}", conversation);
            }
        }
    }
}
