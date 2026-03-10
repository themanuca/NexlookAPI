public class LookDTO
{
    public Guid Id { get; set; }
    public string Titulo { get; set; }
    public string Descricao { get; set; }
    public DateTime DataCriacao { get; set; }
    public List<LookImageDTO> Images { get; set; }
}

public class LookImageDTO
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; }
}