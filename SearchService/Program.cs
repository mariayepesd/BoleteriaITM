using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Qdrant.Client;
using Qdrant.Client.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// ─── Clientes como singletons ────────────────────────────────────────────────

var esUrl    = builder.Configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
var qdHost   = builder.Configuration["Qdrant:Host"]       ?? "localhost";
var qdPort   = int.Parse(builder.Configuration["Qdrant:Port"] ?? "6334");

builder.Services.AddSingleton(_ =>
    new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(esUrl))));

builder.Services.AddSingleton(_ =>
    new QdrantClient(qdHost, qdPort));

var app = builder.Build();

app.MapOpenApi();
app.MapHealthChecks("/health");

// ─── Indexación al arrancar (con reintentos) ─────────────────────────────────

_ = Task.Run(async () =>
{
    await Task.Delay(8000); // esperar a que ES y Qdrant estén listos
    for (var intento = 1; intento <= 5; intento++)
    {
        try
        {
            var esClient  = app.Services.GetRequiredService<ElasticsearchClient>();
            var qdClient  = app.Services.GetRequiredService<QdrantClient>();
            await IndexarEventosAsync(esClient, qdClient);
            app.Logger.LogInformation("[Search] Indexación completada en intento {n}", intento);
            break;
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("[Search] Intento {n}/5 fallido: {msg}", intento, ex.Message);
            await Task.Delay(intento * 3000);
        }
    }
});

// ─── Endpoints ───────────────────────────────────────────────────────────────

// GET /api/search/text?q=rock
// Búsqueda por texto usando Elasticsearch — busca en nombre, sede, categoría y descripción
app.MapGet("/api/search/text", async (string q, ElasticsearchClient esClient) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { Error = "El parámetro 'q' es requerido" });

    var response = await esClient.SearchAsync<EventoDoc>(s => s
        .Index("eventos")
        .Query(qry => qry
            .Bool(b => b
                .Should(
                    sh => sh.Match(m => m.Field(f => f.Nombre)      .Query(q).Boost(3)),
                    sh => sh.Match(m => m.Field(f => f.Descripcion) .Query(q).Boost(1.5f)),
                    sh => sh.Match(m => m.Field(f => f.Sede)        .Query(q).Boost(2)),
                    sh => sh.Match(m => m.Field(f => f.Categoria)   .Query(q))
                )
                .MinimumShouldMatch(1)
            )
        )
        .Size(5)
    );

    if (!response.IsValidResponse)
        return Results.Problem("Elasticsearch no está disponible");

    var resultados = response.Hits.Select(h => new
    {
        h.Score,
        h.Source?.Id,
        h.Source?.Nombre,
        h.Source?.Sede,
        h.Source?.Categoria,
        h.Source?.Descripcion,
        Motor = "Elasticsearch"
    });

    return Results.Ok(new { Query = q, Motor = "Elasticsearch (texto exacto)", Resultados = resultados });
})
.WithName("BusquedaTexto");

// GET /api/search/semantic?q=musica electronica fiesta
// Búsqueda semántica usando Qdrant — entiende el "vibe" del usuario
app.MapGet("/api/search/semantic", async (string q, QdrantClient qdClient) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { Error = "El parámetro 'q' es requerido" });

    var queryVector = CalcularVectorVibe(q.ToLowerInvariant());

    try
    {
        var hits = await qdClient.SearchAsync("eventos", queryVector, limit: 3);

        var resultados = hits.Select(h => new
        {
            Score     = Math.Round(h.Score, 4),
            Id        = h.Id.Num,
            Nombre    = h.Payload.TryGetValue("nombre",    out var n) ? n.StringValue : "",
            Sede      = h.Payload.TryGetValue("sede",      out var s) ? s.StringValue : "",
            Categoria = h.Payload.TryGetValue("categoria", out var c) ? c.StringValue : "",
            Motor     = "Qdrant"
        });

        return Results.Ok(new
        {
            Query        = q,
            Motor        = "Qdrant (búsqueda semántica por IA)",
            VectorUsado  = queryVector,
            Resultados   = resultados
        });
    }
    catch
    {
        return Results.Problem("Qdrant no está disponible");
    }
})
.WithName("BusquedaSemantica");

// POST /api/search/index — Re-indexa todos los eventos manualmente
app.MapPost("/api/search/index", async (ElasticsearchClient esClient, QdrantClient qdClient) =>
{
    await IndexarEventosAsync(esClient, qdClient);
    return Results.Ok(new { Message = "Re-indexación completada", EventosIndexados = VibeVector.SeedEventos.Count });
})
.WithName("ReIndexar");

app.Run();

// ─── Lógica de indexación ─────────────────────────────────────────────────────

static async Task IndexarEventosAsync(ElasticsearchClient esClient, QdrantClient qdClient)
{
    // ── Elasticsearch ──────────────────────────────────────────────────────────
    var existeIndex = await esClient.Indices.ExistsAsync("eventos");
    if (!existeIndex.Exists)
        await esClient.Indices.CreateAsync("eventos");

    foreach (var ev in VibeVector.SeedEventos)
    {
        await esClient.IndexAsync(ev, idx => idx.Index("eventos").Id(ev.Id));
    }

    // ── Qdrant ─────────────────────────────────────────────────────────────────
    var colecciones = await qdClient.ListCollectionsAsync();
    if (!colecciones.Any(c => c == "eventos"))
    {
        await qdClient.CreateCollectionAsync("eventos",
            new VectorParams { Size = VibeVector.Dimensions, Distance = Distance.Cosine });
    }

    var puntos = VibeVector.SeedEventos.Select(ev => new PointStruct
    {
        Id      = (ulong)ev.Id,
        Vectors = ev.VibeVector,
        Payload =
        {
            ["nombre"]    = ev.Nombre,
            ["sede"]      = ev.Sede,
            ["categoria"] = ev.Categoria,
            ["id"]        = ev.Id
        }
    }).ToList();

    await qdClient.UpsertAsync("eventos", puntos);
}

// ─── Vectorización de queries ─────────────────────────────────────────────────
//
// Espacio de 8 dimensiones (vibes):
//   0=energía  1=electrónica  2=tropical/latino  3=rock
//   4=clásica  5=fiesta       6=internacional     7=familiar

static float[] CalcularVectorVibe(string query)
{
    var v = new float[VibeVector.Dimensions];

    var mapa = new Dictionary<string, (int dim, float peso)>
    {
        ["energia"]       = (0, 1.0f), ["energetico"] = (0, 0.9f), ["intenso"]   = (0, 0.7f),
        ["electronica"]   = (1, 1.0f), ["electro"]    = (1, 0.9f), ["techno"]    = (1, 0.9f), ["dj"] = (1, 0.7f),
        ["tropical"]      = (2, 1.0f), ["cumbia"]     = (2, 0.9f), ["salsa"]     = (2, 0.9f),
        ["colombia"]      = (2, 0.8f), ["medellin"]   = (2, 0.7f), ["caribe"]    = (2, 0.7f),
        ["rock"]          = (3, 1.0f), ["metal"]      = (3, 0.9f), ["punk"]      = (3, 0.8f), ["guitarra"] = (3, 0.6f),
        ["clasic"]        = (4, 1.0f), ["clasica"]    = (4, 1.0f), ["opera"]     = (4, 0.9f), ["formal"] = (4, 0.7f),
        ["fiesta"]        = (5, 1.0f), ["rumba"]      = (5, 0.9f), ["party"]     = (5, 0.9f), ["bailar"] = (5, 0.8f), ["diversion"] = (5, 0.7f),
        ["mundial"]       = (6, 1.0f), ["internacional"] = (6, 1.0f), ["global"] = (6, 0.9f), ["madrid"] = (6, 0.6f),
        ["familia"]       = (7, 1.0f), ["familiar"]   = (7, 1.0f), ["niños"]    = (7, 0.9f), ["suave"] = (7, 0.5f),
        ["vibe"]          = (0, 0.5f), // "vibe" activa energía + fiesta
    };

    // Activar fiesta cuando se menciona "vibe"
    if (query.Contains("vibe")) v[5] += 0.5f;

    var palabras = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    foreach (var palabra in palabras)
    {
        foreach (var (keyword, (dim, peso)) in mapa)
        {
            if (palabra.Contains(keyword))
                v[dim] += peso;
        }
    }

    // Normalizar para que el vector tenga magnitud 1 (requerido por cosine similarity)
    var magnitud = MathF.Sqrt(v.Sum(x => x * x));
    if (magnitud > 0)
        for (var i = 0; i < v.Length; i++)
            v[i] /= magnitud;

    // Si la query no activa ninguna dimensión, devolver vector de demanda alta (default)
    if (magnitud == 0)
        return new float[] { 0.6f, 0.3f, 0.5f, 0.3f, 0.2f, 0.7f, 0.6f, 0.4f };

    return v;
}

// ─── Seed data (espeja el seed de InventoryService) ──────────────────────────

static class VibeVector
{
    public const int Dimensions = 8;

    public static readonly List<EventoDoc> SeedEventos =
    [
        // ── Medellín ──
        new(1, "Festival de los Dos Mundos", "Medellín", "General",
            "El evento más grande de Colombia. Música tropical, rock y electrónica en el corazón de Medellín.",
            new[] { 0.80f, 0.20f, 0.90f, 0.10f, 0.00f, 0.90f, 0.70f, 0.60f }),

        new(2, "Festival de los Dos Mundos", "Medellín", "VIP",
            "Acceso VIP con áreas exclusivas, barra libre y vista preferencial al escenario principal.",
            new[] { 0.60f, 0.30f, 0.80f, 0.10f, 0.40f, 0.60f, 0.90f, 0.30f }),

        new(3, "Festival de los Dos Mundos", "Medellín", "Palco",
            "Experiencia Palco: zona reservada para los coleccionistas. Canapés, sommelier y producción clásica.",
            new[] { 0.50f, 0.10f, 0.70f, 0.00f, 0.70f, 0.40f, 0.90f, 0.40f }),

        // ── Madrid ──
        new(4, "Festival de los Dos Mundos", "Madrid", "General",
            "La energía del rock madrileño se fusiona con electrónica de talla mundial. Noche de alta intensidad.",
            new[] { 0.90f, 0.50f, 0.20f, 0.80f, 0.10f, 0.90f, 0.80f, 0.40f }),

        new(5, "Festival de los Dos Mundos", "Madrid", "VIP",
            "Disfruta el festival con acceso VIP, zona de networking y backstage para los mayores artistas de rock.",
            new[] { 0.70f, 0.40f, 0.20f, 0.60f, 0.40f, 0.70f, 0.90f, 0.30f }),

        new(6, "Festival de los Dos Mundos", "Madrid", "Palco",
            "Palco exclusivo con vista panorámica. El ambiente más sofisticado del festival europeo.",
            new[] { 0.40f, 0.20f, 0.10f, 0.30f, 0.90f, 0.40f, 0.90f, 0.50f })
    ];
}

record EventoDoc(
    int     Id,
    string  Nombre,
    string  Sede,
    string  Categoria,
    string  Descripcion,
    float[] VibeVector   // no se indexa en ES, solo en Qdrant
);
