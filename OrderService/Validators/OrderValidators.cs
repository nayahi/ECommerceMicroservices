using FluentValidation;
using OrderService.DTOs;

namespace OrderService.Validators
{
    public class CreateOrderValidator : AbstractValidator<CreateOrderDto>
    {
        public CreateOrderValidator()
        {
            RuleFor(x => x.UserId)
                .GreaterThan(0).WithMessage("Usuario valido es requerido");

            RuleFor(x => x.ShippingAddress)
                .NotEmpty().WithMessage("Direccion de envio es requerida")
                .MaximumLength(500).WithMessage("Direccion de envio debe tener al menos 500 caracteres");

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("El pedido debe tener al menos un item");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId)
                    .GreaterThan(0).WithMessage("Producto valido es requerido");

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0");
            });

            RuleFor(x => x.PaymentMethod)
                .NotEmpty().WithMessage("Metodo de pago valido es requerido");
        }
    }
}
