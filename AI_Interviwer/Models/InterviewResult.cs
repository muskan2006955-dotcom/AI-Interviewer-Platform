using System;
using System.Collections.Generic;

namespace AI_Interviwer.Models;

public partial class InterviewResult
{
    public int ResultId { get; set; }

    public int? SessionId { get; set; }

    public int ConfidenceScore { get; set; }

    public int DurationMinutes { get; set; }

    public int TechnicalScore { get; set; }

    public string OverallFeedback { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual InterviewSession? Session { get; set; }
}
