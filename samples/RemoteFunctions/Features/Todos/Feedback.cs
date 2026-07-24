namespace RemoteFunctions.Features.Todos;

using FluentValidation;

public record Feedback(string Message, int Rating);

/// <summary>
/// A plain FluentValidation validator — registered in Program.cs, it runs
/// automatically before SubmitFeedback executes (AddSvelteNetFluentValidation).
/// </summary>
public class FeedbackValidator : AbstractValidator<Feedback>
{
	public FeedbackValidator()
	{
		RuleFor(f => f.Message).NotEmpty().MinimumLength(5)
			.WithMessage("Tell us a little more (at least 5 characters).");
		RuleFor(f => f.Rating).InclusiveBetween(1, 5)
			.WithMessage("Rating must be between 1 and 5.");
	}
}
