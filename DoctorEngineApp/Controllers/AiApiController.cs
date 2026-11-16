using DoctorEngineApp.Localization;
using DoctorEngineApp.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    [Route("api/ai")]
    [ApiController]
    public class AiApiController : ControllerBase
    {
        private readonly AiAssistantService _ai;
        private readonly ILogger<AiApiController> _logger;

        public AiApiController(AiAssistantService ai, ILogger<AiApiController> logger)
        {
            _ai = ai;
            _logger = logger;
        }

        public class ChatRequest
        {
            public List<AiMessage>? Messages { get; set; }
        }

        /// <summary>
        /// Sual göndərir; cavab mətn axını (text/plain, hissə-hissə) şəklində qayıdır.
        /// Antiforgery token "RequestVerificationToken" header-i ilə göndərilməlidir.
        /// </summary>
        [HttpPost("chat")]
        [RequestSizeLimit(64 * 1024)]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
        {
            if (!_ai.IsConfigured)
                return StatusCode(503, new { error = L.T("AI köməkçi hələ ayarlanmayıb. Zəhmət olmasa ustalarımızla əlaqə saxlayın.") });

            var history = AiAssistantService.SanitizeHistory(request.Messages, out var validationError);
            if (history == null)
                return BadRequest(new { error = validationError });

            var limit = _ai.TryConsume(HttpContext, out var quota);
            Response.Headers["X-AI-Remaining"] = quota.Remaining.ToString();
            Response.Headers["X-AI-Limit"] = quota.Limit.ToString();

            switch (limit)
            {
                case AiLimitResult.DailyLimit:
                    return StatusCode(429, new { error = L.T("Bugünkü {0} sual limitiniz doldu. Sabah yenidən yazın və ya ustalarımızla əlaqə saxlayın.", quota.Limit), remaining = 0 });
                case AiLimitResult.TooFast:
                    return StatusCode(429, new { error = L.T("Çox tez-tez yazırsınız. Bir dəqiqə gözləyib yenidən cəhd edin."), remaining = quota.Remaining });
                case AiLimitResult.SiteLimit:
                    return StatusCode(429, new { error = L.T("AI köməkçinin bugünkü ümumi limiti doldu. Sabah yenidən cəhd edin və ya ustalarımızla əlaqə saxlayın."), remaining = 0 });
            }

            await using var chunks = _ai.StreamAsync(history, ct).GetAsyncEnumerator(ct);

            // Birinci hissəni gözlə: xəta cavab başlamazdan əvvəl baş verərsə JSON qaytar
            bool hasFirst;
            try
            {
                hasFirst = await chunks.MoveNextAsync();
            }
            catch (AiException ex)
            {
                _ai.Refund(HttpContext);   // xidmət xətası istifadəçinin limitindən düşülmür
                Response.Headers["X-AI-Remaining"] = _ai.GetQuota(HttpContext).Remaining.ToString();
                return StatusCode((int)ex.Status, new { error = ex.Message });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new EmptyResult();
            }

            Response.StatusCode = 200;
            Response.ContentType = "text/plain; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache, no-store";
            Response.Headers["X-Accel-Buffering"] = "no"; // nginx arxasında buferləmə olmasın
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            try
            {
                if (!hasFirst)
                {
                    await Response.WriteAsync(L.T("Bağışlayın, cavab hazırlaya bilmədim. Sualı bir az fərqli yazmağa cəhd edin."), ct);
                    return new EmptyResult();
                }

                do
                {
                    await Response.WriteAsync(chunks.Current, ct);
                    await Response.Body.FlushAsync(ct);
                }
                while (await chunks.MoveNextAsync());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // İstifadəçi "Dayandır" basdı və ya səhifəni bağladı
            }
            catch (AiException ex)
            {
                _logger.LogWarning("AI cavabı yarımçıq kəsildi: {Message}", ex.Message);
                await Response.WriteAsync(L.T("\n\n_(Cavab yarımçıq qaldı. Yenidən cəhd edin.)_"), CancellationToken.None);
            }

            return new EmptyResult();
        }
    }
}
