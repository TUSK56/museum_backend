namespace VirtualMuseum.API.DTOs;

public sealed record AiChatLogEntryDto(
    Guid Id,
    string UserLabel,
    string UserMessage,
    string AssistantReply,
    string Source,
    DateTime CreatedAt,
    bool FromN8n);

public sealed record AiChatTopQuestionDto(string Question, int Count);

public sealed record AiChatLogStatsDto(
    int TotalQueries,
    int QueriesToday,
    int SuccessfulReplies,
    IReadOnlyList<AiChatTopQuestionDto> TopQuestions);

public sealed record AiChatLogsResponseDto(
    AiChatLogStatsDto Stats,
    IReadOnlyList<AiChatLogEntryDto> Entries);
