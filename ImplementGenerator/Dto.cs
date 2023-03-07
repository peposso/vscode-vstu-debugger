namespace ImplementGenerator;

public sealed class Dto
{
    public PropertyDto[] Properties { get; set; } = Array.Empty<PropertyDto>();

    public MethodDto[] Methods { get; set; } = Array.Empty<MethodDto>();
    public string? Namespace { get; set; }
    public string TypeName { get; set; } = "";
    public string Field { get; set; } = "field";
}

public sealed class PropertyDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public sealed class MethodDto
{
    public string Name { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public ParameterDto[] Parameters { get; set; } = Array.Empty<ParameterDto>();
}

public class ParameterDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string ByRef { get; set; } = "";
}
