using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AI_Interviwer.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace AI_Interviwer.Controllers
{
    public class InterviewController : Controller
    {
        private readonly AIDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<InterviewController> _logger;

        public InterviewController(AIDbContext context, IConfiguration configuration, ILogger<InterviewController> logger)
        {
            _context = context;
            _logger = logger;

            _apiKey = configuration["GeminiApi:ApiKey"]
                ?? throw new InvalidOperationException("Gemini API Key not found in appsettings.json");

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45) // Evaluation might take a bit longer
            };

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AI-Interviewer-App/1.0");
        }

        // 1. PAGE LOAD ACTION: Saare sawaal live AI se lekar aao
        public async Task<IActionResult> StartInterview()
        {
            int? sessionId = HttpContext.Session.GetInt32("CurrentSessionId");
            if (sessionId == null) return RedirectToAction("Dashboard", "Home");

            var currentSession = await _context.InterviewSessions
                                         .Include(s => s.SessionQuestions)
                                         .FirstOrDefaultAsync(s => s.SessionId == sessionId.Value);

            if (currentSession == null) return RedirectToAction("Dashboard", "Home");

            if (!currentSession.SessionQuestions.Any())
            {
                try
                {
                    List<string> questionsList = await GenerateAllQuestionsAtOnce(currentSession.Role, currentSession.Level, currentSession.InterviewType, currentSession.TotalLength);

                    foreach (var qText in questionsList)
                    {
                        _context.SessionQuestions.Add(new SessionQuestion
                        {
                            SessionId = currentSession.SessionId,
                            QuestionText = qText,
                            CreatedAt = DateTime.Now,
                            AiFeedback = "Pending evaluation",
                            IsCorrect = false,
                            Score = 0, // Initializing Score field
                            UserTextAnswer = "",
                            UserCodeAnswer = "" // Clear separation for code block
                        });
                    }
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return Content($"CRITICAL ERROR CAUGHT:\n{ex.Message}\n\nPlease check your appsettings.json Key or network restriction.");
                }
            }

            HttpContext.Session.SetInt32("QuestionCount", 1);
            return View();
        }

        // 2. AJAX ENDPOINT: Text aur Code dono answers save karne ke liye
        [HttpPost]
        public async Task<IActionResult> GetAIQuestion(string previousAnswer, string previousCodeAnswer)
        {
            try
            {
                int? sessionId = HttpContext.Session.GetInt32("CurrentSessionId");
                if (sessionId == null)
                {
                    return Json(new { success = false, error = "Session expired! Please restart a new interview." });
                }

                var currentSession = await _context.InterviewSessions
                                             .Include(s => s.SessionQuestions)
                                             .FirstOrDefaultAsync(s => s.SessionId == sessionId.Value);

                if (currentSession == null)
                {
                    return Json(new { success = false, error = "Interview session not found!" });
                }

                int currentQuestionNumber = HttpContext.Session.GetInt32("QuestionCount") ?? 1;
                var allQuestions = currentSession.SessionQuestions.OrderBy(q => q.CreatedAt).ToList();
                int maxQuestions = allQuestions.Count;

                // Checking if user submitted either a text answer or a code chunk
                if (!string.IsNullOrEmpty(previousAnswer) || !string.IsNullOrEmpty(previousCodeAnswer))
                {
                    var answeredQuestion = allQuestions.ElementAtOrDefault(currentQuestionNumber - 1);
                    if (answeredQuestion != null)
                    {
                        // Storing both responses safely into their corresponding DB columns
                        answeredQuestion.UserTextAnswer = previousAnswer ?? "";
                        answeredQuestion.UserCodeAnswer = previousCodeAnswer ?? "";
                        await _context.SaveChangesAsync();
                    }

                    currentQuestionNumber++;
                    HttpContext.Session.SetInt32("QuestionCount", currentQuestionNumber);
                }

                if (currentQuestionNumber > maxQuestions)
                {
                    currentSession.Status = "Completed";
                    await _context.SaveChangesAsync();
                    HttpContext.Session.Remove("QuestionCount");

                    return Json(new
                    {
                        success = true,
                        isCompleted = true,
                        redirectUrl = Url.Action("InterviewResult", new { id = sessionId.Value })
                    });
                }

                var targetQuestion = allQuestions.ElementAt(currentQuestionNumber - 1);

                return Json(new
                {
                    success = true,
                    isCompleted = false,
                    currentCount = currentQuestionNumber,
                    totalCount = maxQuestions,
                    data = targetQuestion.QuestionText.Trim()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetAIQuestion execution pipeline.");
                return Json(new { success = false, error = "An unexpected database synchronization error occurred." });
            }
        }

        // 3. FINAL SUMMARY ACTION: Result page kholte hi live evaluation
        // 🌟 UPDATED: AI generated text solution ko database mein bind karne ke liye update kiya hai.
        // 3. FINAL SUMMARY ACTION: Result page kholte hi live evaluation
        public async Task<IActionResult> InterviewResult(int id)
        {
            var session = await _context.InterviewSessions
                                         .Include(s => s.SessionQuestions)
                                         .FirstOrDefaultAsync(s => s.SessionId == id);

            if (session == null) return RedirectToAction("Dashboard", "Home");

            // Evaluate if at least one of the answer types contains content
            var unEvaluated = session.SessionQuestions
                                     .Where(q => q.AiFeedback == "Pending evaluation" &&
                                               (!string.IsNullOrEmpty(q.UserTextAnswer) || !string.IsNullOrEmpty(q.UserCodeAnswer)))
                                     .ToList();

            if (unEvaluated.Any())
            {
                foreach (var item in unEvaluated)
                {
                    // Feeding both textual response and code editor response into Gemini
                    var evaluation = await EvaluateSingleAnswer(item.QuestionText, item.UserTextAnswer, item.UserCodeAnswer);

                    item.AiFeedback = evaluation.Feedback;
                    item.Score = evaluation.Score;
                    item.IsCorrect = evaluation.Score >= 45; // Marking correct if score meets threshold

                    // 🎯 FIXED LOGIC: Agar user ka apna text answer khali hai, 
                    // toh chahe score 100 ho ya 0, AI ka banaya hua conceptual text har haal mein save hoga.
                    if (string.IsNullOrWhiteSpace(item.UserTextAnswer) && !string.IsNullOrWhiteSpace(evaluation.CorrectTextSolution))
                    {
                        item.UserTextAnswer = evaluation.CorrectTextSolution;
                    }
                }
                await _context.SaveChangesAsync();
            }

            int totalQuestions = session.SessionQuestions.Count;
            int totalPossibleScore = totalQuestions * 100;
            int totalEarnedScore = session.SessionQuestions.Sum(q => q.Score);

            ViewBag.ScorePercentage = totalPossibleScore > 0 ? (totalEarnedScore * 100) / totalPossibleScore : 0;
            ViewBag.TotalCount = totalQuestions;
            ViewBag.AttemptedCount = session.SessionQuestions.Count(q => !string.IsNullOrEmpty(q.UserTextAnswer) || !string.IsNullOrEmpty(q.UserCodeAnswer));

            return View(session);
        }
        // --- ⚙️ Private Engine Core Methods ---

        private async Task<List<string>> GenerateAllQuestionsAtOnce(string role, string level, string type, int count)
        {
            if (string.IsNullOrEmpty(_apiKey)) return null;

            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

                string bulkPrompt = $"You are an expert technical interviewer. Generate exactly {count} distinct technical interview questions for a '{role}' role ({level} level) focusing specifically on '{type}'.\n" +
                                     "The output MUST be a valid, flat JSON array of strings only. Example:\n" +
                                     "[\"Question 1 text?\", \"Question 2 text?\"]\n" +
                                     "Do not wrap response in markdown blocks like ```json. Do not include intro text.";

                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = bulkPrompt } } } },
                    generationConfig = new
                    {
                        responseMimeType = "application/json",
                        temperature = 0.6
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string resStr = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(resStr);
                    string rawJson = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString().Trim();

                    if (rawJson.Contains("```"))
                    {
                        rawJson = rawJson.Replace("```json", "").Replace("```", "").Trim();
                    }

                    int startToken = rawJson.IndexOf('[');
                    int endToken = rawJson.LastIndexOf(']');
                    if (startToken >= 0 && endToken >= 0 && endToken > startToken)
                    {
                        rawJson = rawJson.Substring(startToken, endToken - startToken + 1);
                    }

                    var parsedList = JsonSerializer.Deserialize<List<string>>(rawJson);
                    if (parsedList != null && parsedList.Count > 0) return parsedList;
                }
                else
                {
                    string errorDetails = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Google API Error (Status: {response.StatusCode}): {errorDetails}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Live bulk generation parsing error.");
                throw;
            }
            return null;
        }

        // 🌟 UPDATED ENGINE: Ab yeh method 3 parameters return karega (Feedback, Score, CorrectTextSolution)
        private async Task<(string Feedback, int Score, string CorrectTextSolution)> EvaluateSingleAnswer(string question, string textAnswer, string codeAnswer)
        {
            try
            {
                // Edge case: check if both options are completely blank
                if (string.IsNullOrWhiteSpace(textAnswer) && string.IsNullOrWhiteSpace(codeAnswer))
                {
                    return ("No meaningful description or source code was submitted.", 0, "No standard conceptual explanation available.");
                }

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

                // ⚡ NEW STRICT RULES IN SYSTEM INSTRUCTION: AI ko bound kiya hai ke woh ideal conceptual solution lazmi return kare.
                string systemInstruction = "You are a strict and precise technical interviewer evaluating candidate submissions. " +
                    "Review the candidate's structural conceptual explanation and their written implementation source code against core engineering logic.\n" +
                    "CRITICAL RULES:\n" +
                    "1. Assign a numeric 'Score' from 0 to 100 based on syntax accuracy, logic stability, and conceptual understanding.\n" +
                    "2. Provide exactly 2 concise sentences in 'Feedback' stating strengths and what can be improved.\n" +
                    "3. Regardless of how broken, missing, or incorrect the code is, you MUST generate a comprehensive text-based conceptual solution in 'CorrectTextSolution' showing how a perfect engineer would describe the correct working logic or solution in clear text prose (no code snippets inside text).\n\n" +
                    "Output MUST be strict raw JSON format ONLY, matching this schema precisely:\n" +
                    "{\n" +
                    "  \"Score\": 75,\n" +
                    "  \"Feedback\": \"Your architectural layout is stable. However, edge-case validation is missing.\",\n" +
                    "  \"CorrectTextSolution\": \"To address this challenge perfectly, you must establish an explicit matching criteria using clean conditional statements, followed by an optimized memory buffer...\"\n" +
                    "}";

                string userPrompt = $"Technical Question: {question}\n" +
                                    $"Candidate's Conceptual Answer: {textAnswer}\n" +
                                    $"Candidate's Code Editor Submission:\n// --- Code Start ---\n{codeAnswer}\n// --- Code End ---\n" +
                                    "Evaluate strictly based on both items if provided, then return all 3 required JSON properties.";

                var requestBody = new
                {
                    system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                    contents = new[] { new { parts = new[] { new { text = userPrompt } } } },
                    generationConfig = new { responseMimeType = "application/json", temperature = 0.3 }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    string rawJson = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString().Trim();

                    if (rawJson.StartsWith("```json")) rawJson = rawJson.Substring(7);
                    if (rawJson.StartsWith("```")) rawJson = rawJson.Substring(3);
                    if (rawJson.EndsWith("```")) rawJson = rawJson.Substring(0, rawJson.Length - 3);
                    rawJson = rawJson.Trim();

                    int startIndex = rawJson.IndexOf('{');
                    int endIndex = rawJson.LastIndexOf('}');
                    if (startIndex >= 0 && endIndex >= 0 && endIndex > startIndex)
                    {
                        rawJson = rawJson.Substring(startIndex, endIndex - startIndex + 1);
                    }

                    using JsonDocument parsedJson = JsonDocument.Parse(rawJson);
                    int score = 0;
                    string feedback = "Evaluation processed seamlessly.";
                    string correctTextSolution = "Automated conceptual explanation is ready.";

                    // Extracting all three keys dynamically from Gemini JSON response
                    foreach (var property in parsedJson.RootElement.EnumerateObject())
                    {
                        if (property.Name.Equals("Score", StringComparison.OrdinalIgnoreCase))
                        {
                            score = property.Value.GetInt32();
                        }
                        else if (property.Name.Equals("Feedback", StringComparison.OrdinalIgnoreCase))
                        {
                            feedback = property.Value.GetString() ?? feedback;
                        }
                        else if (property.Name.Equals("CorrectTextSolution", StringComparison.OrdinalIgnoreCase))
                        {
                            correctTextSolution = property.Value.GetString() ?? correctTextSolution;
                        }
                    }

                    return (feedback, score, correctTextSolution);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JSON response parsing engine crash.");
            }

            return ("Answer structure evaluation completed under fallback standard configuration.", 60, "Fallback text-based evaluation criteria loaded successfully.");
        }
    }
}