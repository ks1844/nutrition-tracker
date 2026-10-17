var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

// WebApplicationFactory（API テスト）から参照するため
public partial class Program;
