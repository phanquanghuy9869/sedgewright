IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Sedgewright>("sedgewright");
builder.Build().Run();
