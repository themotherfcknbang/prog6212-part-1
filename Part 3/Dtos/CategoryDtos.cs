using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// Used for both create (POST) and update (PUT)
public class CategoryRequestDto
{
    [Required, StringLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    // Left out of a request, this stays 0 and fails the range check,
    // so a distance is effectively required.
    [Range(0.01, 999.99)]
    public decimal DistanceKM { get; set; }

    [Range(0, 999999.99)]
    public decimal EntryFee { get; set; }

    [Range(1, int.MaxValue)]
    public int? MaxParticipants { get; set; }
}