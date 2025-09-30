using CatalogService.DTOs;
using FluentValidation;

public class CreateProductValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nombre de producto es requerido")
            .MaximumLength(100).WithMessage("Nombre producto debe ser menor a 100 caracteres");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Descripcion del producto es requerido")
            .MaximumLength(500).WithMessage("La descripcion no puede exceder los 500 caracteres");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("El precio debe ser mayor a 0");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("Stock no puede ser negativo");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("La categoria es requerida")
            .MaximumLength(50).WithMessage("La category no debe ser mayor a 50 caracteres");
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Price)
            .GreaterThan(0).When(x => x.Price.HasValue);

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).When(x => x.Stock.HasValue);

        RuleFor(x => x.Category)
            .MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Category));
    }
}