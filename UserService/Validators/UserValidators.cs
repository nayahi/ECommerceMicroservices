using FluentValidation;
using UserService.DTOs;

namespace UserService.Validators
{
    public class RegisterValidator : AbstractValidator<RegisterDto>
    {
        public RegisterValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Correo es requerido")
                .EmailAddress().WithMessage("Formato invalido de correo");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password es requerido")
                .MinimumLength(8).WithMessage("Password debe ser de al meno 8 caracteres")
                .Matches(@"[A-Z]").WithMessage("Password debe contener al menos una letra mayuscula")
                .Matches(@"[a-z]").WithMessage("Password debe contener al menos una letra minuscula")
                .Matches(@"[0-9]").WithMessage("Password debe contener al menos un numero");

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Nombre es requerido")
                .MaximumLength(50).WithMessage("Nombre no puede ser mayor a 50 caracteres");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Apellido es requerido")
                .MaximumLength(50).WithMessage("Apellido no puede ser mayor a 50 caracteres");
        }
    }

    public class LoginValidator : AbstractValidator<LoginDto>
    {
        public LoginValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Correo es requerido")
                .EmailAddress().WithMessage("Formato invalido de correo");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password es requerido");
        }
    }
}
