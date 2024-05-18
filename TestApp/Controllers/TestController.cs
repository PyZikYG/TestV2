﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TestApp.Data;
using TestApp.Models;
using TestApp.Models.AnswerViewModels;
using TestApp.Models.TestViewModels;

namespace TestApp.Controllers
{
    public partial class TestController : Controller
    {

        #region Конструктор

        public TestController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            // IEmailSender emailSender,
            //ISmsSender smsSender,
            ILoggerFactory loggerFactory
        )
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            //_emailSender = emailSender;
            //_smsSender = smsSender;
            _logger = loggerFactory.CreateLogger<UserController>();
        }

        #endregion Конструктор

        #region Удаление

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [Route("/Tests/{testId}/Delete/")]
        public async Task<IActionResult> Delete(int testId)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleOrDefaultAsync(t => t.Id == testId);
            if (test.CreatedBy != user) return Forbid();
            test.IsDeleted = true;
            _context.Update(test);
            await _context.SaveChangesAsync();
            return RedirectToAction("Tests");
        }

        #endregion Удаление

        #region Список тестов

        [HttpGet]
        [Authorize]
        [Route("/Tests/")]
        public async Task<IActionResult> Tests()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var createdTests = _context.Tests.Where(t => t.CreatedBy.Id == user.Id && !t.IsDeleted).ToList();
            //if (createdTests == null) //return View(new ICollection<Test>);
            var addTestModel = new AddTestModel
            {
                Model1 = createdTests,
                Model2 = new AddTestViewModel()
            };
            return View(addTestModel);
        }

        #endregion Список тестов

        #region Детали

        [HttpGet]
        [Authorize]
        [Route("/Tests/{id}/")]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests
                .SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (test == null) return NotFound();
            var questions = await _context.Questions
                .Where(q => q.Test == test && !q.IsDeleted).ToListAsync();
            ViewBag.Questions = questions;
            ViewData["user"] = user;
            if (test.CreatedBy == user)
            {
                try
                {
                    var link = Url.Link("AddTestToUser", new { testId = test.Id });
                    var qrCode = "data:image/png;base64, " + Utils.Utils.GenerateBase64QRCodeFromLink(link);
                    ViewBag.qrCodeBase64 = qrCode;
                }
                catch (DllNotFoundException)
                {
                    ViewBag.qrCodeBase64 = "";
                }

                return View(test);
            }

            var testResult = await _context.TestResults.Where(r => r.Test == test && r.CompletedByUser == user)
                .FirstAsync();
            // у пользователя отсутствует тест
            if (testResult == null) return RedirectToAction("AddTestToUser", new { testId = test.Id, userId = user.Id });
            ViewData["testResult"] = testResult;

            return View(test);
        }

        #endregion Детали

        #region Поля

        public static readonly float EPSILON = 0.0000001F;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        private readonly SignInManager<User> _signInManager;

        //private readonly IEmailSender _emailSender;
        //private readonly ISmsSender _smsSender;
        private readonly ILogger _logger;

        #endregion Поля

        #region Добавление

        [HttpGet]
        [Route("/Tests/Add/")]
        [Authorize]
        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [Route("/Tests/Add/")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AddTestModel model)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (ModelState.IsValid)
            {
                var test = new Test
                {
                    Name = model.Model2.Name,
                    CreatedBy = user,
                    IsEnabled = model.Model2.IsEnabled,
                    Shuffled = model.Model2.Shuffled,
                    HideRightAnswers = !model.Model2.HideRightAnswers,
                    Count = Math.Abs(model.Model2.Count),
                    TimeToPassing = Math.Abs(model.Model2.Time)
                };
                await _context.Tests.AddAsync(test);

                // Добавить тест к пользователю, который его создал (чтобы он тоже мог проходить его)
                var testResult = new TestResult
                {
                    IsCompleted = false,
                    Test = test,
                    CompletedByUser = user
                    //TotalQuestions = (uint)test.Questions.Count()
                };
                user.TestResults.Add(testResult);

                await _context.SaveChangesAsync();
                return RedirectToAction("Details", new { id = test.Id });
            }

            return View(model);
        }

        [HttpGet]
        [Route("/Tests/AddFromFile/")]
        [Authorize]
        public IActionResult AddFromFile()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [Route("/Tests/AddFromFile/")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFromFile(IFormFile uploadedFile)
        {
            try
            {
                var user = await _userManager.GetUserAsync(HttpContext.User);
                // get a stream
                var stream = uploadedFile.OpenReadStream();
                var testData = Parser.Parse(Tokenizer.Tokenize(new StreamReader(stream)));
                testData.Test.CreatedBy = user;
                await _context.Tests.AddAsync(testData.Test);
                var testResult = new TestResult
                {
                    IsCompleted = false,
                    Test = testData.Test,
                    CompletedByUser = user
                };
                user.TestResults.Add(testResult);
                await _context.SaveChangesAsync();
                foreach (var q in testData.Questions) await _context.Questions.AddAsync(q);
                await _context.SaveChangesAsync();
                foreach (var c in testData.Codes)
                {
                    var code = (await _context.Codes.AddAsync(c)).Entity;
                    var question = code.Question as CodeQuestion;
                    question.Code = code;
                }
                await _context.SaveChangesAsync();
                foreach (var o in testData.Options)
                {
                    await _context.Options.AddAsync(o);
                    if (o.Question is SingleChoiceQuestion && o.IsRight)
                    {
                        var questionCreated =
                            _context.Questions.Single(q => q.Id == o.Question.Id) as SingleChoiceQuestion;
                        questionCreated.RightAnswer = o;
                        _context.Questions.Update(questionCreated);
                    }
                }

                // Добавить тест к пользователю, который его создал (чтобы он тоже мог проходить его)

                await _context.SaveChangesAsync();
                return RedirectToAction("Details", new { id = testData.Test.Id });
            }
            catch (Exception e)
            {
                ViewBag.Exception = e.Message;
                return View();
            }
        }

        [HttpPost]
        [Authorize]
        [Route("/User/[controller]s/{testId}/AddTestToUser/")]
        public async Task<IActionResult> AddTestToUser(int testId)
        {
            //throw new NotImplementedException();
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleOrDefaultAsync(t => t.Id == testId && !t.IsDeleted);
            if (user == null) Response.StatusCode = 404;
            if (test == null) Response.StatusCode = 404;
            if (!test.IsEnabled) Response.StatusCode = 400;

            if (_context.TestResults.Any(t => t.CompletedByUser == user && t.Test == test)) Response.StatusCode = 400;

            var testResult = new TestResult
            {
                IsCompleted = false,
                Test = test,
                CompletedByUser = user
                //TotalQuestions = (uint)test.Questions.Count()
            };
            await _context.TestResults.AddAsync(testResult);
            await _context.SaveChangesAsync();
            Response.StatusCode = 200;
            return RedirectToAction("Start", new { testResultId = testResult.Id });
        }

        [HttpGet]
        [Authorize]
        [Route("/User/[controller]s/{testId}/AddTestToUser/", Name = "AddTestToUser")]
        public async Task<IActionResult> AddTestToUser(AddTestToUserViewModel model, int testId)
        {
            var test = await _context.Tests.SingleOrDefaultAsync(t => t.Id == testId && !t.IsDeleted);
            if (test == null)
            {
                Response.StatusCode = 404;
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(HttpContext.User);
            var testResult =
                await _context.TestResults.SingleOrDefaultAsync(tr => tr.Test == test && tr.CompletedByUser == user);
            if (testResult != null) return RedirectToAction("Start", new { testResultId = testResult.Id });
            ViewData["test"] = test;
            return View(model);
        }

        [HttpPost]
        [Authorize]
        [Route("/User/[controller]s/AddTestToUserAjax/")]
        public async Task<JsonResult> AddTestToUserAjax(int testId, int userId)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleOrDefaultAsync(x => x.Id == testId && !x.IsDeleted);
            if (user == null)
            {
                Response.StatusCode = 404;
                return new JsonResult("Пользователь с данным ID не найден");
            }

            if (test == null)
            {
                Response.StatusCode = 404;
                return new JsonResult("Тест с данным ID не найден");
            }

            if (!test.IsEnabled)
            {
                Response.StatusCode = 400;
                return new JsonResult("Тест не включен");
            }

            if (_context.TestResults.Any(t => t.CompletedByUser == user && t.Test == test))
            {
                Response.StatusCode = 400;
                return new JsonResult("Тест уже добавлен");
            }

            var testResult = new TestResult
            {
                IsCompleted = false,
                Test = test,
                CompletedByUser = user
                //TotalQuestions = (uint)test.Questions.Count()
            };
            await _context.TestResults.AddAsync(testResult);
            await _context.SaveChangesAsync();
            Response.StatusCode = 200;
            return new JsonResult("Успешно");
        }

        [HttpPost]
        [Authorize]
        [Route("/User/[controller]s/{testResultId}/ReAdd/")]
        public async Task<IActionResult> ReAdd(int testResultId)
        {
            //throw new NotImplementedException();
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var testResult = await _context.TestResults.Include(t => t.Test).SingleOrDefaultAsync(t => t.Id == testResultId);
            if (user == null) Response.StatusCode = 404;
            if (testResult == null) Response.StatusCode = 404;
            if (!testResult.Test.IsEnabled) Response.StatusCode = 400;
            _context.Answers.RemoveRange(_context.Answers.Where(a => a.TestResult == testResult));
            testResult.IsCompleted = false;
            _context.TestResults.Update(testResult);
            await _context.SaveChangesAsync();
            Response.StatusCode = 200;
            return RedirectToAction("Results", new { id = testResult.Test.Id });
        }

        #endregion Добавление

        #region Включение/Выключение

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [Route("/Tests/{testId}/Enable/")]
        public async Task<IActionResult> Enable(int testId)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleOrDefaultAsync(t => t.Id == testId && !t.IsDeleted);
            if (test.CreatedBy != user) return Forbid();
            test.IsEnabled = !test.IsEnabled;
            _context.Tests.Update(test);
            await _context.SaveChangesAsync();
            return RedirectToAction("Tests");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [Route("/Tests/{testId}/Shuffle/")]
        public async Task<IActionResult> Shuffle(int testId)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleOrDefaultAsync(t => t.Id == testId && !t.IsDeleted);
            if (test.CreatedBy != user) return Forbid();
            test.Shuffled = !test.Shuffled;
            _context.Tests.Update(test);
            await _context.SaveChangesAsync();
            return RedirectToAction("Tests");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [Route("/Tests/{testId}/Hide/")]
        public async Task<IActionResult> Hide(int testId)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleOrDefaultAsync(t => t.Id == testId && !t.IsDeleted);
            if (test.CreatedBy != user) return Forbid();
            test.HideRightAnswers = !test.HideRightAnswers;
            _context.Tests.Update(test);
            await _context.SaveChangesAsync();
            return RedirectToAction("Tests");
        }

        [HttpGet]
        [Authorize]
        [Route("/Tests/{testId}/Count/")]
        public async Task<IActionResult> Count(int testId)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleAsync(t => t.Id == testId && !t.IsDeleted);
            if (test.CreatedBy != user) return Forbid();
            return View(test);
        }

        [HttpPost]
        [Authorize]
        [Route("/Tests/{testId}/Count/")]
        public async Task<IActionResult> Count(int testId, Test model)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleAsync(t => t.Id == testId && !t.IsDeleted);
            test.Count = Math.Abs(model.Count);
            test.TimeToPassing = Math.Abs(model.TimeToPassing);
            _context.Update(test);
            await _context.SaveChangesAsync();
            return RedirectToAction("Tests");
        }

        #endregion Включение/Выключение

        #region Результаты

        [HttpGet]
        [Authorize]
        [Route("/Tests/Results/")]
        public async Task<IActionResult> TestResults()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var tests = _context.TestResults.Where(t => t.CompletedByUser == user)
                .Include(a => a.Test).ThenInclude(b => b.CreatedBy).ToList();
            return View(tests);
        }

        [HttpGet]
        [Authorize]
        [Route("/[controller]/Result/{testResultId}/Details/")]
        public async Task<IActionResult> TestResultDetails(int testResultId)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var testResult = await _context.TestResults.Include(tr => tr.Test)
                .SingleAsync(tr => tr.Id == testResultId && tr.CompletedByUser == user);
            if (testResult == null) return NotFound();
            if (!testResult.Test.IsEnabled || !testResult.IsCompleted) return Forbid();
            ViewBag.AnswerId = _context.Answers.Where(a => a.TestResult == testResult)
                .SingleOrDefault(a => a.Order == 1).Id;
            return View(testResult);
        }

        [HttpGet]
        [Authorize]
        [Route("/Tests/{id}/Results")]
        public async Task<IActionResult> Results(int id, int? searchId)
        {
            ViewData["searchId"] = searchId;
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleOrDefaultAsync(t => t.Id == id);
            var testResults = _context.TestResults.Where(tr => tr.Test == test && tr.IsCompleted)
                .Include(tr => tr.Answers)
                .Include(tr => tr.CompletedByUser);
            if (searchId != null)
                testResults = testResults.Where(tr => tr.CompletedByUserId == searchId).Include(tr => tr.Answers)
                    .Include(tr => tr.CompletedByUser);
            var model = new TestResultsModel
            {
                Results = await testResults
                    .AsNoTracking()
                    .ToListAsync(),
                Test = test
            };
            return View(model);
        }

        [HttpGet]
        [Authorize]
        [Route("/Tests/{id}/Stats/")]
        public async Task<IActionResult> Stats(int id)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var test = await _context.Tests.SingleOrDefaultAsync(t => t.Id == id);
            if (test.CreatedById != user.Id) Forbid();
            var questions = _context.Questions.Where(q => q.TestId == test.Id && !q.IsDeleted);
            var stat = new Stat { TestName = test.Name, TestId = test.Id };
            foreach (var question in questions)
            {
                QuestionStat questionStat = new QuestionStat
                {
                    QuestionId = question.Id,
                    QuestionType = question.GetTypeString(),
                    QuestionTitle = question.Title
                };
                var answersForQuestion = _context.Answers.Where(a => a.QuestionId == question.Id);
                questionStat.MaxScore = question.Score;
                float averageSum = 0, derivationSum = 0;
                int n = 0;
                foreach (var answer in answersForQuestion)
                {
                    switch (answer.Result)
                    {
                        case null:
                            {
                                questionStat.NullCount++;
                                break;
                            }
                        case AnswerResult.Wrong:
                            {
                                questionStat.WrongCount++;
                                break;
                            }
                        case AnswerResult.PartiallyRight:
                            {
                                questionStat.PartiallyRightCount++;
                                break;
                            }
                        case AnswerResult.Right:
                            {
                                questionStat.RightCount++;
                                break;
                            }
                    }
                    n++;
                    averageSum += answer.Score;
                    derivationSum += answer.Score * answer.Score;
                }

                var average = averageSum / n;
                var derivationSumAverage = derivationSum / n;
                var stDer = Math.Sqrt(derivationSumAverage - (average * average));
                questionStat.AverageScore = (float)Math.Round(average, 2);
                questionStat.ScoreStandartDerivation = (float)Math.Round(stDer, 2);
                stat.QuestionStats.Add(questionStat);
            }

            stat.MostDifficult = questions.SingleOrDefault(x => x.Id == stat.GetMostDifficultId());
            stat.MostEasy = questions.SingleOrDefault(x => x.Id == stat.GetMostEasyId());
            return View("Stat", stat);
        }

        #endregion Результаты

        #region Прохождение

        [Authorize]
        [HttpGet]
        [Route("/[controller]/Result/{testResultId}/Start/")]
        public async Task<IActionResult> Start(int testResultId)
        {
            ViewBag.IsStarted = false;
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var testResult = await _context.TestResults.Include(t => t.Test)
                .SingleAsync(tr => tr.Id == testResultId && tr.CompletedByUser == user);
            if (testResult == null) return NotFound();
            if (!testResult.Test.IsEnabled) return Forbid();
            if (_context.Answers.Any(a => a.TestResult == testResult))
            {
                ViewBag.IsStarted = true;
                ViewBag.AnswerId = _context.Answers.Where(a => a.TestResult == testResult)
                    .SingleOrDefault(a => a.Order == 1).Id;
            }

            ViewBag.UserId = user.Id;
            if (ViewBag.IsStarted)
                ViewBag.QuestionsCount = _context.Answers.Count(a => a.TestResult == testResult);
            else
            {
                ViewBag.QuestionsCount =
                    _context.Questions.Where(q => !q.IsDeleted).Count(q => q.Test == testResult.Test);
                if (testResult.Test.Count != 0 && testResult.Test.Shuffled && testResult.Test.Count < ViewBag.QuestionsCount)
                    ViewBag.QuestionsCount = testResult.Test.Count;
            }
            return View(testResult);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateScore(int scoreNow)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (user != null)
            {
                user.ScoreNow = scoreNow;
                _context.Update(user);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "User not found" });
        }


        // POST
        [Authorize]
        [HttpPost]
        [Route("/[controller]/Result/{testResultId}/Start/")]
        public async Task<IActionResult> Start(int testResultId, ErrorViewModel model)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var testResult = await _context.TestResults
                .Include(tr => tr.Test)
                .ThenInclude(t => t.Questions)
                .SingleAsync(t => t.Id == testResultId);
            if (testResult == null) return NotFound();
            if (!testResult.Test.IsEnabled) return Forbid();
            var questions = testResult.Test.Questions.Where(q => !q.IsDeleted).ToList();
            if (questions.Count() == 0) return NotFound();
            if (_context.Answers.Any(a => a.TestResult == testResult))
                return RedirectToAction("Answer", "Answer",
                    new
                    {
                        testResultId = testResult.Id,
                        answerId = _context.Answers.Where(a => a.TestResult == testResult)
                            .SingleOrDefault(a => a.Order == 1).Id
                    });
            var answers = new List<Answer>();
            Answer answer = null;
            if (testResult.Test.Count != 0 && testResult.Test.Count < questions.Count && testResult.Test.Shuffled)
            {
                var order = new ushort[testResult.Test.Count];
                for (var i = 0; i < order.Length; i++)
                    order[i] = (ushort)(i + 1);
                if (testResult.Test.Shuffled)
                    Shuffle(order);
                var j = 0;
                Shuffle(questions);
                for (var k = 0; k < order.Length; k++)
                {
                    var question = questions[k];
                    switch (question.QuestionType)
                    {
                        case "SingleChoiceQuestion":
                            answer = new SingleChoiceAnswer();
                            break;

                        case "MultiChoiceQuestion":
                            answer = new MultiChoiceAnswer();
                            break;

                        case "TextQuestion":
                            answer = new TextAnswer();
                            break;

                        case "DragAndDropQuestion":
                            answer = new DragAndDropAnswer();
                            break;

                        case "CodeQuestion":
                            answer = new CodeAnswer();
                            break;
                    }

                    if (answer == null) throw new NullReferenceException();
                    answer.Result = null;
                    answer.Question = question;
                    answer.Score = 0;
                    answer.TestResult = testResult;
                    answer.Order = order[j++];
                    await _context.Answers.AddAsync(answer);
                    answers.Add(answer);
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                var order = new ushort[questions.Count()];
                for (var i = 0; i < order.Length; i++)
                    order[i] = (ushort)(i + 1);
                if (testResult.Test.Shuffled)
                    Shuffle(order);
                var j = 0;
                foreach (var question in questions)
                {
                    switch (question.QuestionType)
                    {
                        case "SingleChoiceQuestion":
                            answer = new SingleChoiceAnswer();
                            break;

                        case "MultiChoiceQuestion":
                            answer = new MultiChoiceAnswer();
                            break;

                        case "TextQuestion":
                            answer = new TextAnswer();
                            break;

                        case "DragAndDropQuestion":
                            answer = new DragAndDropAnswer();
                            break;

                        case "CodeQuestion":
                            answer = new CodeAnswer();
                            break;
                    }

                    if (answer == null) throw new NullReferenceException();
                    answer.Question = question;
                    answer.Score = 0;
                    answer.TestResult = testResult;
                    answer.Order = order[j++];
                    await _context.Answers.AddAsync(answer);
                    answers.Add(answer);
                    await _context.SaveChangesAsync();
                }
            }
            testResult.StartedOn = DateTime.UtcNow;
            _context.TestResults.Update(testResult);
            await _context.SaveChangesAsync();
            // TODO: redirect to first answer (question)
            //throw new NotImplementedException();
            return RedirectToAction("Answer", "Answer",
                new { testResultId = testResult.Id, answerId = answers.SingleOrDefault(a => a.Order == 1).Id });
        }

        [Authorize]
        [HttpGet("/Test/Result/{testResultId}/GetScore/")]
        public async Task<IActionResult> GetScore(int TestResultId, [FromQuery] int questionId, [FromQuery] int selected)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var correctAnswer = _context.Options.Where(x => x.QuestionId.Equals(questionId)).Where(z => z.IsRight.Equals(true));
            float score = 0;
            if (correctAnswer.Count() > 0)
            {
                foreach (Option q in correctAnswer)
                {
                    if (q.Id == selected)
                    {
                        var questionData = _context.Questions.Where(x => x.Id == questionId).FirstOrDefault();
                        score += questionData.Score;
                    }
                }
                return Ok(score);
            }
            return Ok(0);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        [Authorize]
        [Route("/Test/Result/{testResultId}/Finish/")]
        public async Task<IActionResult> FinishTest(int testResultId)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var testResult = await _context.TestResults.SingleAsync(tr => tr.Id == testResultId);
            if (testResult.CompletedByUser != user) return BadRequest();
            if (testResult.IsCompleted) return BadRequest();
            testResult.IsCompleted = true;
            testResult.CompletedOn = DateTime.UtcNow;
            uint count = 0;
            //var questions = testResult.Test.Questions.ToList();
            var answers = _context.Answers.Where(a => a.TestResult == testResult);
            testResult.TotalQuestions = (uint)answers.Count();
            float _allscores = 0;
            foreach (var answer in answers)
            {
                var _question = await _context.Questions
                    .SingleOrDefaultAsync(q => q.Id == answer.QuestionId);

                if (answer is SingleChoiceAnswer singleChoiceAnswer)
                {
                    _allscores += GetSingleChoiceResult(singleChoiceAnswer, _question, ref count);
                }
                else if (answer is MultiChoiceAnswer multiChoiceAnswer)
                {
                    _allscores += GetMultiChoiceResult(_question, multiChoiceAnswer, ref count);
                }
                else if (answer is TextAnswer textAnswer)
                {
                    _allscores += GetTextResult(_question, textAnswer, ref count);
                }
                else if (answer is DragAndDropAnswer dndAnswer)
                {
                    _allscores += GetDragAndDropResult(_question, dndAnswer, ref count);
                }
                else if (answer is CodeAnswer codeAnswer)
                {
                    _allscores += GedCodeResult(_question, codeAnswer, ref count);
                }
                await _context.SaveChangesAsync();
            }

            testResult.RightAnswersCount = count;
            testResult.TotalScore = _allscores;
            _context.Update(testResult);
            await _context.SaveChangesAsync();
            //throw new NotImplementedException();
            return RedirectToAction("TestResults");
        }

        private float GedCodeResult(Question _question, CodeAnswer codeAnswer, ref uint count)
        {
            var question = _question as CodeQuestion;
            //.Include(a => a.Question)
            //.ThenInclude(q => (q as CodeQuestion).Code)
            //.Include(a => a.Code).Include(a => a.Option)
            _context.Entry(question).Reference(x => x.Code).Load();
            _context.Entry(codeAnswer).Reference(x => x.Code).Load();
            var userCode = codeAnswer.Code?.Value == null ? "" : codeAnswer.Code.Value;
            var creatorCode = question.Code.Value;
            var creatorArgs = question.Code.Args;
            var userOutput = Compile(userCode, creatorArgs);
            var creatorOutput = Compile(creatorCode, creatorArgs);
            var code = _context.Codes.SingleOrDefault(c => c.Answer == codeAnswer);
            if (code == null)
            {
                code = (_context.Add(new Code() { Answer = codeAnswer, Value = userCode })).Entity;
                codeAnswer.Code = code;
                _context.Update(codeAnswer);
            }

            code.Args = creatorArgs;
            code.Output = userOutput;
            _context.Codes.Update(code);
            if (userCode != null)
            {
                if (userOutput == creatorOutput)
                {
                    codeAnswer.Score = codeAnswer.Question.Score;
                    count++;
                    codeAnswer.Result = AnswerResult.Right;
                }
                else
                {
                    codeAnswer.Result = AnswerResult.Wrong;
                    codeAnswer.Score = 0;
                }
            }
            else
            {
                codeAnswer.Result = null;
                codeAnswer.Score = 0;
            }

            _context.Update(codeAnswer);
            return codeAnswer.Score;
        }

        private float GetDragAndDropResult(Question _question, DragAndDropAnswer dndAnswer, ref uint count)
        {
            float score = 0;
            var question = _question as DragAndDropQuestion;
            _context.Entry(question).Collection(x => x.Options).Load();
            _context.Entry(dndAnswer).Collection(x => x.DragAndDropAnswerOptions).Load();
            if (dndAnswer.DragAndDropAnswerOptions == null || dndAnswer.DragAndDropAnswerOptions.Count == 0)
            {
                dndAnswer.Result = null;
                dndAnswer.Score = 0;
            }
            else
            {
                var dndOptions = dndAnswer.DragAndDropAnswerOptions;
                int optionsCount = dndOptions.Count(), wrongOrderCount = 0;
                foreach (var dndOption in dndOptions)
                    if (dndOption.RightOptionId != dndOption.OptionId)
                        wrongOrderCount++;
                score = question.Score *
                           (optionsCount - wrongOrderCount) / (float)optionsCount;
                dndAnswer.Score = score > 0 ? score : 0;

                if (Math.Abs(dndAnswer.Score - question.Score) < EPSILON)
                {
                    count++;
                    dndAnswer.Result = AnswerResult.Right;
                }
                else
                {
                    dndAnswer.Result = score == 0 ? AnswerResult.Wrong : AnswerResult.PartiallyRight;
                }
            }
            _context.Update(dndAnswer);
            return score;
        }

        private float GetTextResult(Question _question, TextAnswer textAnswer, ref uint count)
        {
            var question = _question as TextQuestion;
            if (!String.IsNullOrEmpty(textAnswer.Text) && !String.IsNullOrEmpty(question.TextRightAnswer))
            {
                if (String.Equals(textAnswer.Text, question.TextRightAnswer, StringComparison.CurrentCultureIgnoreCase))
                {
                    textAnswer.Score = question.Score;
                    count++;
                    textAnswer.Result = AnswerResult.Right;
                }
                else
                {
                    textAnswer.Result = AnswerResult.Wrong;
                    textAnswer.Score = 0;
                }
            }
            else
            {
                textAnswer.Result = null;
                textAnswer.Text = "";
                textAnswer.Score = 0;
            }
            _context.TextAnswers.Update(textAnswer);
            return textAnswer.Score;
        }

        private float GetMultiChoiceResult(Question _question, MultiChoiceAnswer multiChoiceAnswer, ref uint count)
        {
            float _allScores = 0;
            var question = _question as MultiChoiceQuestion;
            _context.Entry(question).Collection(x => x.Options).Load();
            _context.Entry(multiChoiceAnswer).Collection(x => x.AnswerOptions).Load();
            // count Score
            int countChecked = 0, countWrong = 0;
            float countOptions = multiChoiceAnswer.AnswerOptions.Count;
            if (multiChoiceAnswer.AnswerOptions == null || multiChoiceAnswer.AnswerOptions.Count == 0)
            {
                multiChoiceAnswer.Score = 0;
                multiChoiceAnswer.Result = null;
            }
            else
            {
                foreach (var answerOption in multiChoiceAnswer.AnswerOptions)
                {
                    var rightAnswer = question.Options.Single(o => o.Id == answerOption.OptionId).IsRight;
                    if (rightAnswer) countChecked++;
                    if (answerOption.Checked != rightAnswer) countWrong++;
                }
                var score = question.Score *
                            (countChecked - countWrong) / (float)countChecked;
                multiChoiceAnswer.Score = score;
                _allScores = score;
                if (Math.Abs(multiChoiceAnswer.Score - question.Score) < EPSILON)
                {
                    count++;
                    multiChoiceAnswer.Result = AnswerResult.Right;
                }
                else
                {
                    if (Math.Abs(multiChoiceAnswer.Score) < EPSILON)
                    {
                        multiChoiceAnswer.Result = AnswerResult.Wrong;
                    }
                    else multiChoiceAnswer.Result = AnswerResult.PartiallyRight;
                }
            }

            _context.MultiChoiceAnswers.Update(multiChoiceAnswer);
            return _allScores;
        }

        private float GetSingleChoiceResult(SingleChoiceAnswer singleChoiceAnswer, Question _question, ref uint count)
        {
            _context.Entry(singleChoiceAnswer).Reference(x => x.Option).Load();
            float _allScore = 0;
            var question = _question as SingleChoiceQuestion;
            _context.Entry(question).Reference(x => x.RightAnswer).Load();
            singleChoiceAnswer.Score = 0;
            if (singleChoiceAnswer.Option != null)
            {
                if (singleChoiceAnswer.Option == question.RightAnswer)
                {
                    singleChoiceAnswer.Score = question.Score;
                    count++;
                    singleChoiceAnswer.Result = AnswerResult.Right;
                    _allScore += singleChoiceAnswer.Score;
                }
                else singleChoiceAnswer.Result = AnswerResult.Wrong;
            }
            else singleChoiceAnswer.Result = null;
            _context.SingleChoiceAnswers.Update(singleChoiceAnswer);
            return _allScore;
        }

        #endregion Прохождение

        #region Вспомогательные методы

        private static readonly Random Random = new Random();

        public static void Shuffle<T>(List<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = Random.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static void Shuffle<T>(T[] list)
        {
            var n = list.Length;
            while (n > 1)
            {
                n--;
                var k = Random.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static string Compile(string code, string args)
        {
            var TimeoutSeconds = 5;
            var output = new StringBuilder();
            object[] Args;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var assemblyName = Path.GetRandomFileName();
            MetadataReference[] references =
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    true,
                    optimizationLevel: OptimizationLevel.Release,
                    generalDiagnosticOption: ReportDiagnostic.Error,
                    warningLevel: 0));
            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (var diagnostic in failures)
                        output.AppendFormat("{0}: {1}\n", diagnostic.Id, diagnostic.GetMessage());
                }
                else
                {
                    try
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        var assembly = Assembly.Load(ms.ToArray());
                        var type = assembly.GetType("TestsApp.Program");
                        var obj = Activator.CreateInstance(type);
                        var method = type.GetMethod("Main");
                        var parameters = method.GetParameters();
                        var types = new List<Type>();
                        foreach (var p in parameters) types.Add(p.ParameterType);
                        var multiArgs = args.Split(';').Select(arg => arg.Trim()).ToArray();
                        string[] tmp;
                        foreach (var a in multiArgs)
                        {
                            tmp = a.Split(',').Select(arg => arg.Trim()).ToArray();
                            if (!string.IsNullOrEmpty(a))
                            {
                                Args = new object[tmp.Length];
                                for (var i = 0; i < tmp.Length; i++) Args[i] = Convert.ChangeType(tmp[i], types[i]);
                            }
                            else
                            {
                                Args = null;
                            }

                            var task = Task.Run(() => type.InvokeMember("Main",
                                BindingFlags.Default | BindingFlags.InvokeMethod,
                                null,
                                obj,
                                Args));
                            task.Wait(TimeSpan.FromSeconds(TimeoutSeconds));
                            if (task.IsCompleted)
                                output.AppendLine(task.Result.ToString());
                            else
                                throw new TimeoutException("Timed out 5sec");
                        }
                    }
                    catch (TimeoutException)
                    {
                        output = new StringBuilder($"TimeoutException: max {TimeoutSeconds * 1000} ms.");
                    }
                    catch (Exception e)
                    {
                        output = new StringBuilder(e.Message);
                    }
                }
            }

            return output.ToString();
        }

        #endregion Вспомогательные методы
    }
}