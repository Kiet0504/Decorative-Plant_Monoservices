using decorativeplant_be.Application.Common.DTOs.RoomScan;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using decorativeplant_be.Application.Features.RoomScan.Commands;
using decorativeplant_be.Application.Features.RoomScan.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Application.Features.RoomScan.Handlers;

public sealed class RoomScanCommandHandler : IRequestHandler<RoomScanCommand, RoomScanResultDto>
{
    private readonly IRoomScanGeminiClient _gemini;
    private readonly IRoomScanCatalogRankingService _catalogRanking;
    private readonly RoomScanHandlerOptions _options;
    private readonly AiRoutingSettings _aiRouting;

    public RoomScanCommandHandler(
        IRoomScanGeminiClient gemini,
        IRoomScanCatalogRankingService catalogRanking,
        IOptions<RoomScanHandlerOptions> options,
        IOptions<AiRoutingSettings> aiRouting)
    {
        _gemini = gemini;
        _catalogRanking = catalogRanking;
        _options = options.Value;
        _aiRouting = aiRouting.Value;
    }

    public async Task<RoomScanResultDto> Handle(RoomScanCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var mime = string.IsNullOrWhiteSpace(req.ImageMimeType) ? "image/jpeg" : req.ImageMimeType.Trim();
        var pipelineMode = RoomScanPipelineModeParser.FromApiValue(_options.PipelineMode);

        var profile = await _gemini.AnalyzeRoomFromImageAsync(req.ImageBase64, mime, pipelineMode, cancellationToken);
        if (profile == null)
        {
            if (_aiRouting.UseGeminiOnly)
            {
                throw new BadRequestException(
                    "Could not analyze the photo. With AiRouting:UseGeminiOnly, configure AiDiagnosis:GeminiApiKey or RoomScan:GeminiApiKey, " +
                    "ensure the API can reach Google Generative Language API, and try a clearer image.");
            }

            throw pipelineMode == RoomScanPipelineMode.LocalOnly
                ? new BadRequestException(
                    "Could not analyze the photo in full local mode. Ensure Ollama is running (Ollama:BaseUrl), " +
                    "RoomScan:OllamaVisionModel is set (e.g. llava:7b), and Ollama:Model or RoomScan:OllamaRankModel is suitable for ranking, then try again.")
                : new BadRequestException(
                    "Could not analyze the photo. Set Gemini (AiDiagnosis:GeminiApiKey or RoomScan:GeminiApiKey), " +
                    "or configure RoomScan:OllamaVisionModel (e.g. llava:7b) with Ollama running (Ollama:BaseUrl), then try a clearer image.");
        }

        var ranking = await _catalogRanking.GetRecommendationsAsync(
            new RoomScanCatalogRankingRequest
            {
                Profile = profile,
                BranchId = req.BranchId,
                MaxPrice = req.MaxPrice,
                PetSafeOnly = req.PetSafeOnly,
                SkillLevel = req.SkillLevel,
                PipelineMode = pipelineMode
            },
            cancellationToken);

        if (ranking.NoMatches)
        {
            return new RoomScanResultDto
            {
                RoomProfile = profile,
                Recommendations = new List<RoomScanRecommendationDto>(),
                Disclaimer =
                    "No in-stock plants matched your filters. Try another branch, raise the price limit, or turn off pet-safe only."
            };
        }

        return new RoomScanResultDto
        {
            RoomProfile = profile,
            Recommendations = ranking.Recommendations,
            Disclaimer =
                "Suggestions are based on your photo and our listings; always confirm light and care on the product page before buying."
        };
    }
}
