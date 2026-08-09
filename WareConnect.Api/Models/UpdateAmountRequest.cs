using System.ComponentModel.DataAnnotations;

namespace WareConnect.Api.Models;

public class UpdateAmountRequest
{
    [Required]
    public decimal Amount { get; set; }
}
