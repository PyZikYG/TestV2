﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TestApp.Controllers;
using TestApp.Models;

namespace TestApp.Data
{
    internal class DbInitializer
    {
        private const string PASSWORD = "Qwerty123";

        private static readonly Random _random = new Random();

        //private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger _logger;
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;

        public DbInitializer(ApplicationDbContext context,
            UserManager<User> userManager, SignInManager<User> signInManager,
            ILogger logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
        }

        // добавляет в текущую базу
        public async void Initialize()
        {
            _context.Database.EnsureCreated();

            await _context.SaveChangesAsync();
        }

        // удаляет базу и создает новую
        public void InitializeNew()
        {
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
            InitializeUsers();
            //InitializeTests();
        }

        private async void InitializeUsers()
        {
            var users = new[]
            {
                new User {UserName = "user1@example.com", Email = "user1@example.com"},
                new User {UserName = "user2@example.com", Email = "user2@example.com"}
            };

            foreach (var user in users)
            {
                var result = await _userManager.CreateAsync(user, "Qwerty123");
                if (result.Succeeded)
                {
                    user.LockoutEnabled = true;
                    user.EmailConfirmed = false;
                    user.TwoFactorEnabled = false;
                    _logger.LogInformation(3, "User created a new account with password.");
                }
                else
                {
                    _logger.LogCritical("Error creating User instance");
                }
            }

            _context.SaveChanges();
        }

        private async void InitializeTests()
        {
            var users = _context.Users.ToList();
            var test1 = new Test {CreatedBy = users[0], Name = "Test1", IsEnabled = true};
            _context.Tests.Add(test1);


            await _context.SaveChangesAsync();
        }

        private User GenerateUser()
        {
            var count = _context.Users.Count();
            var user = new User {UserName = $"user{count}@example.com", Email = $"user{count}@example.com"};
            if ( _userManager.FindByNameAsync(user.UserName).Result != null || 
                 _userManager.FindByEmailAsync(user.Email).Result != null)
            {
                var name = GenerateExampleEmail();
                user.UserName = name;
                user.Email = name;
            }

            var result = _userManager.CreateAsync(user, "Qwerty123").Result;
            if (result.Succeeded)
            {
                user.LockoutEnabled = true;
                user.EmailConfirmed = false;
                user.TwoFactorEnabled = false;
                _logger.LogInformation(3, "User created a new account with password.");
            }
            else
            {
                _logger.LogCritical("Error creating User instance");
            }

            _context.SaveChanges();
            return user;
        }

        public async void GetStatsForTest(int testId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                CompleteTest(testId);
            }
        }
        private void CompleteTest(int testId)
        {
            var user = GenerateUser();
            var test = _context.Tests.Include(t => t.Questions).SingleOrDefault(t => t.Id == testId);
            // add test to user
            var testResult = new TestResult
            {
                CompletedByUser = user,
                IsCompleted = false,
                Test = test
            };
            _context.TestResults.Add(testResult);
            // start test
            StartTest(user, testResult, test);
            uint count = 0;
            _context.Entry(testResult).Collection(x=>x.Answers).Load();
            foreach (var answer in testResult.Answers)
            {
                switch (answer.AnswerType)
                {
                    case "SingleChoiceAnswer": AnswerSingleChoice(answer);
                        CheckSingleChoice(answer,ref count);
                        break;
                    case "MultiChoiceAnswer": AnswerMultiChoice(answer);
                        CheckMultiChoice(answer,ref count);
                        break;
                    case "TextAnswer": AnswerText(answer);
                        CheckText(answer,ref count);
                        break;
                    case "DragAndDropAnswer": AnswerDragAndDrop(answer);
                        CheckDragAndDrop(answer,ref count);
                        break;
                    case "CodeAnswer": AnswerCode(answer);
                        CheckCode(answer,ref count);
                        break;
                }
            }
            testResult.IsCompleted = true;
            testResult.CompletedOn = DateTime.UtcNow;
            testResult.RightAnswersCount = count;
            _context.Update(testResult);
            _context.SaveChanges();
        }

        private void CheckSingleChoice(Answer _answer, ref uint count)
        {
            var answer = _answer as SingleChoiceAnswer;
            var question = _context.SingleChoiceQuestions.Include(x=>x.RightAnswer)
                .SingleOrDefault(x => x.Id == answer.QuestionId);
            if (question == null) throw new NullReferenceException();
            if (answer.OptionId == question.RightAnswer.Id)
            {
                answer.Score = question.Score;
                answer.Result = AnswerResult.Right;
                count++;
            }
            else
            {
                answer.Score = 0;
                answer.Result = AnswerResult.Wrong;
            }

            _context.Update(answer);
        }

        private void CheckMultiChoice(Answer _answer, ref uint count)
        {
            var answer = _answer as MultiChoiceAnswer;
            _context.Entry(answer).Collection(x=>x.AnswerOptions).Load();
            var question = _context.MultiChoiceQuestions.Include(x=>x.Options)
                .SingleOrDefault(x => x.Id == answer.QuestionId);
            if (question == null) throw new NullReferenceException();
            int countChecked = 0, countWrong = 0;
            float countOptions = answer.AnswerOptions.Count;
            if (answer.AnswerOptions == null || answer.AnswerOptions.Count == 0)
            {
                answer.Score = 0;
                answer.Result = null;
            }
            else
            {
                foreach (var answerOption in answer.AnswerOptions)
                {
                    var rightAnswer = question.Options.Single(o => o.Id == answerOption.OptionId).IsRight;
                    if (rightAnswer) countChecked++;
                    if (answerOption.Checked != rightAnswer) countWrong++;
                }

                var score = question.Score *
                            (countChecked - countWrong) / (float) countChecked;
                answer.Score = score > 0 ? score : 0;
                if (Math.Abs(answer.Score - question.Score) < TestController.EPSILON)
                {
                    answer.Result = AnswerResult.Right;
                    count++;
                }
                else
                {
                    if (answer.Score < TestController.EPSILON)
                    {
                        answer.Result = AnswerResult.Wrong;
                    }
                    else answer.Result = AnswerResult.PartiallyRight;
                }
            }

            _context.MultiChoiceAnswers.Update(answer);
        }

        private void CheckText(Answer _answer, ref uint count)
        {
            var answer = _answer as TextAnswer;
            var question = _context.TextQuestions.Include(x=>x.Options)
                .SingleOrDefault(x => x.Id == answer.QuestionId);
            if (question == null) throw new NullReferenceException();
            if (!String.IsNullOrEmpty(answer.Text) && !String.IsNullOrEmpty(question.TextRightAnswer))
            {
                if (String.Equals(answer.Text, question.TextRightAnswer, StringComparison.CurrentCultureIgnoreCase))
                {
                    answer.Score = question.Score;
                    answer.Result = AnswerResult.Right;
                    count++;
                }
                else
                {
                    answer.Result = AnswerResult.Wrong;
                    answer.Score = 0;
                }
            }
            else
            {
                answer.Result = null;
                answer.Text = "";
                answer.Score = 0;
            }
            _context.TextAnswers.Update(answer);
        }

        private void CheckDragAndDrop(Answer _answer, ref uint count)
        {
            throw new NotImplementedException();
        }

        private void CheckCode(Answer _answer, ref uint count)
        {
            throw new NotImplementedException();
        }

        private int GetRandomOptionId(List<Option> options)
        {
            return options[_random.Next(0, options.Count)].Id;
        }
        
        private int [] GetRandomOptionIdArray(List<Option> options)
        {
            int [] optionIds = options.Select(c => c.Id).ToArray();
            var n = options.Count(o => o.IsRight);
            var result = new int[n]; 
            for (int i=0;i<n;i++)
            {
                int randomIdIndex;
                do
                {
                    randomIdIndex= _random.Next(0, optionIds.Length);
                    result[i] = optionIds[randomIdIndex];
                } while (result[i] == 0);
                optionIds[randomIdIndex] = 0;
            }

            return result;
        }
        private void AnswerSingleChoice(Answer _answer)
        {
            var answer = _answer as SingleChoiceAnswer;
            _context.Entry(answer).Reference(x=>x.Question).Load();
            _context.Entry(answer.Question).Collection(x=>x.Options).Load();
            answer.OptionId = GetRandomOptionId(answer.Question.Options);
            _context.Update(answer);
        }

        private void AnswerMultiChoice(Answer _answer)
        {
            var answer = _answer as MultiChoiceAnswer;
            _context.Entry(answer).Reference(x=>x.Question).Load();
            _context.Entry(answer.Question).Collection(x=>x.Options).Load();
            var checkedIds = GetRandomOptionIdArray(answer.Question.Options);
            foreach (var option in answer.Question.Options)
            {
                var answerOption = new AnswerOption
                {
                    AnswerId = answer.Id,
                    Checked = checkedIds.Contains(option.Id),
                    Option = option
                };
                _context.Add(answerOption);
            }
            _context.Update(answer);
        }

        private void AnswerText(Answer _answer)
        {
            var answer = _answer as TextAnswer;
            throw new NotImplementedException();
        }

        private void AnswerDragAndDrop(Answer _answer)
        {
            var answer = _answer as DragAndDropAnswer;
            throw new NotImplementedException();
        }

        private void AnswerCode(Answer _answer)
        {
            var answer = _answer as CodeAnswer;
            throw new NotImplementedException();
        }

        private void StartTest(User user, TestResult testResult, Test test)
        {

            var answers = new List<Answer>();
            Answer answer = null;
            var questions = testResult.Test.Questions.Where(q => !q.IsDeleted).ToList();
            testResult.TotalQuestions = (uint)questions.Count;
            if (test.Count != 0 && test.Count < test.Questions.Count && testResult.Test.Shuffled)
            {
                
                var order = new ushort[testResult.Test.Count];
                for (var i = 0; i < order.Length; i++)
                    order[i] = (ushort) (i + 1);

                var j = 0;
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
                    _context.Answers.Add(answer);
                    answers.Add(answer);
                    _context.SaveChanges();
                }
            }
            else
            {
                var order = new ushort[questions.Count()];
                for (var i = 0; i < order.Length; i++)
                    order[i] = (ushort) (i + 1);

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
                    _context.Answers.Add(answer);
                    answers.Add(answer);
                   _context.SaveChanges();
                }
            }

            testResult.StartedOn = DateTime.UtcNow;
            _context.TestResults.Update(testResult);
            _context.SaveChanges();

        }

        private static string GenerateExampleEmail()
        {
            return RandomString(8) + "@example.com";
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }
    }
}