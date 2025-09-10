using Application.Interfaces;
using Infra.dbContext;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Application.DTOs.IAdto.LookPromptDTO;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Application.Services.IAService
{
    public class IAservice:IAIService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IUploadImagemService _uploadImagemService;
        private readonly ILogger<IAservice> _logger;
        private readonly string promptSystem = "Você é um consultor de moda objetivo e direto. \r\nAnalise as roupas enviadas (imagens) e sugira **1 combinação principal** de look, pronta para ser usada na ocasião informada. \r\n\r\nRegras:\r\n- " +
            "Seja breve e específico (máximo 4 a 5 linhas).\r\n- Liste as peças do look de forma clara.\r\n- Sugira calçado e acessório que completem o estilo.\r\n- Não descreva todas as roupas individualmente, apenas a combinação escolhida.\r\n- Se houver várias opções boas, mostre no máximo 2 variações rápidas. Se possivel, retorne as urls das imagens escolhida\r\n";
        public IAservice(AppDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory, IUploadImagemService upload, ILogger<IAservice> logger)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = _configuration["ApiKeys:OPENAI_API_KEY"];
            _uploadImagemService = upload;
            _logger = logger;
        }

        public async Task<string> GerarDescricaoImagemAsync(Guid usuarioId, string promptUsuario)
        {
            try
            {
                _logger.LogInformation("Iniciando geração de descrição para usuário {UserId}", usuarioId);

                var lookResult = await _uploadImagemService.BuscarLooksUsuarioAsync(usuarioId);

                var looks = lookResult.SelectMany(look => look.Images.Select(image => new ClothingItemDTO
                {
                    Id = look.Id.ToString(),
                    Nome = look.Titulo,
                    Categoria = look.Descricao,
                    Imagem = image.ImageUrl
                })).ToList();

                if (looks == null || looks.Count == 0)
                {
                    return "Nenhum look fornecido.";
                }

                var messages = new List<object>
                {
                    new {
                        role = "system",
                        content = promptSystem
                    },
                    new {
                        role = "user",
                        content = looks.Select(i => new object[]
                        {
                            new { type = "text", text = $"{promptUsuario}" },
                            new { type = "image_url", image_url = new { url = i.Imagem } }
                        }).SelectMany(x => x).ToList()
                    }
                };
                var body = new
                {
                    model = "gpt-4o-mini",
                    messages,
                    max_tokens = 500,
                    temperature = 0.7
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                {
                    Headers = { { "Authorization", $"Bearer {_apiKey}" } },
                    Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("OpenAI-Beta", "assistants=v1");

                _logger.LogDebug("Enviando requisição para OpenAI. URL: {Url}, Prompt: {Prompt}", request.RequestUri, promptUsuario);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro na API OpenAI. Status: {StatusCode}, Response: {Response}", 
                        response.StatusCode, responseContent);
                    throw new Exception($"OpenAI API Error: {responseContent}");
                }

                _logger.LogInformation("Resposta recebida da OpenAI para usuário {UserId}", usuarioId);
                _logger.LogDebug("Resposta completa: {Response}", responseContent);

                return ProcessarRespostaOpenAI(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar descrição de imagem para usuário {UserId}", usuarioId);
                throw;
            }
        }

        public async Task<LookResponse?> GerarDescricaoImagemcomFOTOAsync(Guid usuarioId, string promptUsuario)
        {
            try
            {
                _logger.LogInformation("Iniciando geração de descrição com foto para usuário {UserId}", usuarioId);

                // Buscar roupas do usuário
                var lookResult = await _uploadImagemService.BuscarLooksUsuarioAsync(usuarioId);

                var looks = lookResult.SelectMany(look => look.Images.Select(image => new ClothingItemDTO
                {
                    Id = look.Id.ToString(),
                    Nome = look.Titulo,
                    Categoria = look.Descricao,
                    Imagem = image.ImageUrl
                })).ToList();

                if (looks == null || looks.Count == 0)
                {
                    return null;
                }

                // Prompt estruturado para forçar JSON
                var promptSystem = @"
                        Você é um consultor de moda objetivo. 
                        Analise as roupas enviadas (imagens) e sugira UMA combinação principal de look para a ocasião informada. 

                        Regras:
                        - Responda SEMPRE em JSON válido, no formato:
                        - Use exatamente os valores de id, nome, categoria e url fornecidos pelo usuário, sem inventar.
                        - Não use placeholders como ""URL_da_imagem..."".
                        - Não inclua nada fora do JSON.
                        {
                          ""ocasiao"": ""<texto da ocasião>"",
                          ""descricaoIA"":""<Uma breve explicação>"",
                          ""look"": [
                            { ""id"": ""<id recebido>"", ""nome"": ""<nome recebido>"", ""categoria"": ""<categoria recebida>"", ""imagem"": ""<url recebida>"" }
                          ],
                          ""calcado"": ""<sugestão de calçado>"",
                          ""acessorio"": ""<sugestão de acessório>""
                        }

                        Não inclua nada fora do JSON, nem explicações adicionais.
                        ";

                // Montar mensagens
                var userContent = new List<object>
                {
                    new { type = "text", text = promptUsuario }
                };

                userContent.AddRange(looks.Select(i => new {
                    type = "text",
                    text = $"Peça: {i.Nome}, Categoria: {i.Categoria}, Id: {i.Id}, Url: {i.Imagem}"
                }));

                var messages = new List<object>
                {
                    new { role = "system", content = promptSystem },
                    new { role = "user", content = userContent }
                };

                var body = new
                {
                    model = "gpt-4o-mini",
                    messages,
                    max_tokens = 500,
                    temperature = 0.7
                };

                // Chamada para OpenAI
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                {
                    Headers = { { "Authorization", $"Bearer {_apiKey}" } },
                    Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("OpenAI-Beta", "assistants=v1");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro na API OpenAI. Status: {StatusCode}, Response: {Response}", 
                        response.StatusCode, responseContent);
                    throw new Exception($"OpenAI API Error: {responseContent}");
                }

                _logger.LogInformation("Resposta recebida da OpenAI para usuário {UserId}", usuarioId);
                _logger.LogDebug("Resposta completa: {Response}", responseContent);

                var jsonContent = ProcessarRespostaOpenAI(responseContent);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    return null;
                }

                // Deserializar diretamente para LookResponse
                var lookResponse = JsonSerializer.Deserialize<LookResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return lookResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar descrição de imagem com foto para usuário {UserId}", usuarioId);
                throw;
            }
        }

        private string ProcessarRespostaOpenAI(string responseContent)
        {
            try
            {
                var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (!responseObject.TryGetProperty("choices", out JsonElement choices) ||
                    choices.GetArrayLength() == 0 ||
                    !choices[0].TryGetProperty("message", out JsonElement message) ||
                    !message.TryGetProperty("content", out JsonElement content))
                {
                    _logger.LogWarning("Resposta da OpenAI não contém o conteúdo esperado. Resposta: {Response}", 
                        responseContent);
                    return null;
                }

                var resultado = content.GetString();
                _logger.LogDebug("Conteúdo processado com sucesso: {Content}", resultado);
                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar resposta da OpenAI: {Response}", responseContent);
                throw;
            }
        }
    }
}
