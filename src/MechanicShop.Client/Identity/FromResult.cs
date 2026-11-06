namespace MechanicShop.Client.Identity;

public class FromResult
{
    public bool Succeeded { get; set; }

    public string[] ErrorList { get; set; } = [];
}