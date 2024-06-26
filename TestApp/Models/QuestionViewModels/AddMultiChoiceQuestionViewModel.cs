using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;


namespace TestApp.Models.QuestionViewModels
{
    public class AddMultiChoiceQuestionViewModel : QuestionViewModel
    {

        [Required]
        [OptionsValidation]
        public List<OptionViewModel> Options { get; set; }

        public class OptionsValidationAttribute : ValidationAttribute
        {
            public OptionsValidationAttribute()
            {
                this.ErrorMessage = "В данном типе вопроса можно отметить только один ответ.";
            }

            protected override ValidationResult IsValid(object value, ValidationContext validationContext)
            {
                var options = value as List<OptionViewModel>;
                int countChecked = 0;
                if (options == null) return new ValidationResult("Минимум два варианта ответа в вопросе.");
                if (options.Count < 2) return new ValidationResult("Минимум два варианта ответа в вопросе.");
                foreach (var o in options)
                {
                    if (o.IsRight) countChecked++;
                }
                if (countChecked == 0) return new ValidationResult("Не отмечено ни одного варианта ответа.");
                return ValidationResult.Success;
            }
        }
    }
    
    
}