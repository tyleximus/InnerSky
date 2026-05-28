using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InnerSky.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EmotionProfilesController(InnerSkyDbContext db) : ControllerBase
{
    private static readonly HashSet<string> ValidEmotionIds =
    [
        "joy", "trust", "fear", "surprise", "sadness", "disgust", "anger", "anticipation"
    ];

    [HttpPost]
    public async Task<ActionResult<EmotionProfileResponse>> Create([FromBody] EmotionProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ValidationProblem("Name is required.");

        foreach (var component in request.Components)
        {
            if (!ValidEmotionIds.Contains(component.Emotion))
                return ValidationProblem($"Invalid emotion '{component.Emotion}'.");
            if (component.Level is < 0 or > 2)
                return ValidationProblem($"Invalid level '{component.Level}' for emotion '{component.Emotion}'.");
        }

        if (request.MomentId is null && request.NewMoment is null)
            return ValidationProblem("Provide either MomentId or NewMoment.");
        if (request.MomentId is not null && request.NewMoment is not null)
            return ValidationProblem("Provide only one of MomentId or NewMoment.");

        EmotionMomentEntity moment;
        if (request.MomentId is int momentId)
        {
            var existingMoment = await db.EmotionMoments.FirstOrDefaultAsync(x => x.Id == momentId, cancellationToken);
            if (existingMoment is null)
                return ValidationProblem($"Moment '{momentId}' was not found.");

            moment = existingMoment;
        }
        else
        {
            var title = string.IsNullOrWhiteSpace(request.NewMoment!.Title) ? request.Name.Trim() : request.NewMoment.Title.Trim();
            moment = new EmotionMomentEntity
            {
                
                Title = title,
                Comment = string.IsNullOrWhiteSpace(request.NewMoment.Comment) ? null : request.NewMoment.Comment.Trim(),
                MomentUtc = DateTime.SpecifyKind(request.NewMoment.MomentUtc, DateTimeKind.Utc),
                CreatedUtc = DateTime.UtcNow
            };
            db.EmotionMoments.Add(moment);
        }

        var profile = new EmotionProfileEntity
        {
            
            Name = request.Name.Trim(),
            CreatedUtc = DateTime.UtcNow,
            MomentId = moment.Id,
            Components = request.Components
                .Select(c => new EmotionProfileComponentEntity
                {
                    EmotionId = c.Emotion,
                    IntensityLevel = (byte)c.Level
                })
                .ToList()
        };

        db.EmotionProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

        var response = new EmotionProfileResponse(
            profile.Id,
            profile.Name,
            profile.CreatedUtc,
            profile.MomentId,
            profile.Components.Select(c => new EmotionProfileComponentResponse(c.EmotionId, c.IntensityLevel)).ToList());

        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmotionProfileResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var profile = await db.EmotionProfiles
            .AsNoTracking()
            .Include(x => x.Components)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (profile is null)
            return NotFound();

        return new EmotionProfileResponse(
            profile.Id,
            profile.Name,
            profile.CreatedUtc,
            profile.MomentId,
            profile.Components.Select(c => new EmotionProfileComponentResponse(c.EmotionId, c.IntensityLevel)).ToList());
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmotionProfileResponse>>> List(CancellationToken cancellationToken)
    {
        var profiles = await db.EmotionProfiles
            .AsNoTracking()
            .Include(x => x.Components)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return profiles
            .Select(profile => new EmotionProfileResponse(
                profile.Id,
                profile.Name,
                profile.CreatedUtc,
                profile.MomentId,
                profile.Components.Select(c => new EmotionProfileComponentResponse(c.EmotionId, c.IntensityLevel)).ToList()))
            .ToList();
    }

    [HttpGet("moments")]
    public async Task<ActionResult<IReadOnlyList<EmotionMomentResponse>>> ListMoments(CancellationToken cancellationToken)
    {
        var moments = await db.EmotionMoments
            .AsNoTracking()
            .Include(x => x.Profiles)
                .ThenInclude(x => x.Components)
            .OrderByDescending(x => x.MomentUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return moments.Select(m => new EmotionMomentResponse(
            m.Id,
            m.Title,
            m.Comment,
            m.MomentUtc,
            m.CreatedUtc,
            m.Profiles
                .OrderByDescending(p => p.CreatedUtc)
                .Select(p => new EmotionProfileResponse(
                    p.Id,
                    p.Name,
                    p.CreatedUtc,
                    p.MomentId,
                    p.Components.Select(c => new EmotionProfileComponentResponse(c.EmotionId, c.IntensityLevel)).ToList()))
                .ToList()
        )).ToList();
    }

    private ActionResult ValidationProblem(string detail)
    {
        ModelState.AddModelError("request", detail);
        return base.ValidationProblem(ModelState);
    }
}
