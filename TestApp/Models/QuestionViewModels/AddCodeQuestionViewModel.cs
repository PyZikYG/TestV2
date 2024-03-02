using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TestApp.Models.QuestionViewModels
{
    public class AddCodeQuestionViewModel : QuestionViewModel
    {
        public Code Code { get; set; }
    }
}
