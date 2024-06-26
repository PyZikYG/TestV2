﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Newtonsoft.Json;



namespace TestApp.Models.QuestionViewModels
{

    public class OptionViewModel
    {
        [Required]
        public string Text { get; set; }
        [Required]
        public bool IsRight { get; set; }

        public int? Id { get; set; } = null;

        public int Order { get; set; }
    }


    public class AddSingleChoiceQuestionViewModel : QuestionViewModel
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
                foreach (var option in options)
                {
                    if (option.IsRight) countChecked++;
                }
                if (countChecked > 1 || countChecked == 0)
                {
                    return new ValidationResult("В данном типе вопроса нужно отметить только один ответ.");
                }
                return ValidationResult.Success;

            }
        }
    }
}
