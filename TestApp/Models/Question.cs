using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TestApp.Models
{
    public abstract class Question
    {
        public enum QuestionTypeEnum
        {
            [Display(Name = "С одним правильным ответом")]
            SingleChoiceQuestion = 1,

            [Display(Name = "С несколькими правильными ответами")]
            MultiChoiceQuestion = 2,
            [Display(Name = "С вводом текста")] TextQuestion = 3,

            [Display(Name = "На восстановление последовательности")]
            DragAndDropQuestion = 4,
            [Display(Name = "На написание кода")] CodeQuestion = 5
        }

        public int Id { get; set; }

        [Required]
        [DisplayName("Вопрос")]
        [DataType(DataType.MultilineText)]
        public string Title { get; set; }

        public int TestId { get; set; }

        [Required] public Test Test { get; set; }

        [Required] public string QuestionType { get; set; }

        public List<Option> Options { get; set; }

        [Required] public bool IsDeleted { get; set; } = false;

        [DisplayName("Балл")] public int Score { get; set; }

        public string GetTypeString()
        {
            switch (QuestionType)
            {
                case "SingleChoiceQuestion":
                    return "С одним ответом";
                case "MultiChoiceQuestion":
                    return "С несколькими ответами";
                case "TextQuestion":
                    return "На ввод ответа";
                case "DragAndDropQuestion":
                    return "На последовательность";
                case "CodeQuestion":
                    return "На написание кода";
                default: return "";
            }
        }
    }

    public class SingleChoiceQuestion : Question
    {
        public Option RightAnswer { get; set; }
    }

    public class MultiChoiceQuestion : Question
    {
    }

    public class TextQuestion : Question
    {
        [Required] public string TextRightAnswer { get; set; }
    }

    public class DragAndDropQuestion : Question
    {
    }

    public class CodeQuestion : Question
    {
        public Code Code { get; set; }
    }

    public class Option
    {
        public int Id { get; set; }

        [Required] public string Text { get; set; }

        [Required] public bool IsRight { get; set; }

        public int QuestionId { get; set; }

        [Required] public Question Question { get; set; }

        [Required] public int Order { get; set; }

        public ICollection<AnswerOption> AnswerOptions { get; set; }

        public ICollection<DragAndDropAnswerOption> DropAnswerOptions { get; set; }
    }

    public class Code
    {
        public int Id { get; set; }
        public string Value { get; set; }
        public string Output { get; set; }

        [ForeignKey("QuestionId")] public Question Question { get; set; }

        [ForeignKey("AnswerId")] public Answer Answer { get; set; }

        [ForeignKey("TestId")] public Test Test { get; set; }

        public string Args { get; set; }
    }
}