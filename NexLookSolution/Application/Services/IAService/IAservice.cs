using Application.Interfaces;
using Infra.dbContext;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Application.DTOs.IAdto.LookPromptDTO;

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
        private readonly string promptJsonSystem = @"
                            Você é um consultor de moda. Analise as roupas e retorne APENAS um JSON válido no seguinte formato:
                            {
                                ""ocasiao"": ""texto da ocasião"",
                                ""descricaoIA"": ""breve explicação"",
                                ""look"": [
                                    {
                                        ""id"": ""id da peça"",
                                        ""nome"": ""nome da peça"",
                                        ""categoria"": ""categoria da peça"",
                                        ""imagem"": ""url da imagem""
                                    }
                                ],
                                ""calcado"": ""sugestão de calçado"",
                                ""acessorio"": ""sugestão de acessório""
                            }

                            IMPORTANTE:
                            - Retorne APENAS o JSON, sem explicações adicionais
                            - Não use acentos ou caracteres especiais
                            - Não inclua comentários no JSON
                            - Não use formatação markdown
                            - Mantenha as chaves exatamente como mostrado
                            ";
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

                // Validar todas as imagens
                foreach (var look in looks)
                {
                    if (!await ValidarImagemAsync(look.Imagem))
                    {
                        _logger.LogWarning("Imagem não passou na validação: {ImageUrl}", look.Imagem);
                        return "Uma ou mais imagens não são adequadas para análise.";
                    }
                }

                // Sanitizar o prompt do usuário
                promptUsuario = SanitizarPrompt(promptUsuario);

                var messages = new List<object>
                {
                    new {
                        role = "system",
                        content = promptJsonSystem
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

                // Validar imagens antes de processar
                foreach (var look in looks)
                {
                    if (!await ValidarImagemAsync(look.Imagem))
                    {
                        _logger.LogWarning("Imagem não passou na validação: {ImageUrl}", look.Imagem);
                        return null;
                    }
                }

                promptUsuario = SanitizarPrompt(promptUsuario);

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
                    temperature = 0.9,  // Aumentar para mais variação
                    presence_penalty = 0.6,  // Encoraja a IA a não repetir informações
                    frequency_penalty = 0.6  // Reduz a probabilidade de repetir as mesmas recomendações
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
                _logger.LogDebug("Resposta bruta da OpenAI: {RawResponse}", responseContent);

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
                    _logger.LogWarning("Conteúdo da resposta está vazio");
                    return null;
                }

                try
                {
                    // Tentar limpar possíveis caracteres inválidos
                    jsonContent = jsonContent.Trim()
                                           .TrimStart('`')
                                           .TrimEnd('`')
                                           .Trim();

                    // Verificar se o conteúdo parece ser JSON
                    if (!jsonContent.StartsWith("{") || !jsonContent.EndsWith("}"))
                    {
                        _logger.LogWarning("Resposta não está no formato JSON esperado: {Content}", jsonContent);
                        return null;
                    }

                    // Configurar opções de desserialização
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        NumberHandling = JsonNumberHandling.AllowReadingFromString
                    };

                    _logger.LogDebug("Tentando desserializar JSON: {JsonContent}", jsonContent);

                    var lookResponse = JsonSerializer.Deserialize<LookResponse>(jsonContent, options);

                    if (lookResponse == null)
                    {
                        _logger.LogWarning("Desserialização resultou em null");
                        return null;
                    }

                    // Validar os dados desserializados
                    if (string.IsNullOrWhiteSpace(lookResponse.DescricaoIA) ||
                        lookResponse.Look == null ||
                        !lookResponse.Look.Any())
                    {
                        _logger.LogWarning("Resposta desserializada está incompleta");
                        return null;
                    }

                    return lookResponse;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Erro ao desserializar resposta. Conteúdo: {Content}", jsonContent);

                    // Log detalhado para debug
                    _logger.LogDebug("Caracteres no início do conteúdo: {Start}",
                        string.Join(" ", jsonContent.Take(20).Select(c => ((int)c).ToString("X2"))));

                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar descrição com foto para usuário {UserId}", usuarioId);
                throw;
            }
        }

        private string ProcessarRespostaOpenAI(string responseContent)
        {
            try
            {
                _logger.LogDebug("Iniciando processamento da resposta OpenAI: {Content}", responseContent);

                // Primeiro, tenta deserializar a resposta completa
                var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);
                _logger.LogDebug("Resposta deserializada com sucesso para JsonElement");

                // Validação e extração do conteúdo com logs detalhados
                if (!responseObject.TryGetProperty("choices", out JsonElement choices))
                {
                    _logger.LogWarning("Propriedade 'choices' não encontrada na resposta");
                    return string.Empty;
                }

                if (choices.GetArrayLength() == 0)
                {
                    _logger.LogWarning("Array 'choices' está vazio");
                    return string.Empty;
                }

                var firstChoice = choices[0];
                _logger.LogDebug("Primeiro choice obtido: {Choice}", JsonSerializer.Serialize(firstChoice));

                if (!firstChoice.TryGetProperty("message", out JsonElement message))
                {
                    _logger.LogWarning("Propriedade 'message' não encontrada no primeiro choice");
                    
                    // Tenta encontrar o conteúdo em uma estrutura alternativa
                    if (firstChoice.TryGetProperty("text", out JsonElement text))
                    {
                        var textContent = text.GetString();
                        _logger.LogInformation("Conteúdo encontrado em estrutura alternativa: {Content}", textContent);
                        return textContent ?? string.Empty;
                    }
                    
                    return string.Empty;
                }

                if (!message.TryGetProperty("content", out JsonElement content))
                {
                    _logger.LogWarning("Propriedade 'content' não encontrada na mensagem");
                    return string.Empty;
                }

                var resultado = content.GetString();

                if (string.IsNullOrWhiteSpace(resultado))
                {
                    _logger.LogWarning("Conteúdo da mensagem está vazio após extração");
                    return string.Empty;
                }

                _logger.LogInformation("Conteúdo processado com sucesso. Tamanho: {Length} caracteres", 
                    resultado.Length);
                _logger.LogDebug("Conteúdo final: {Content}", resultado);

                return resultado;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao deserializar resposta da OpenAI");
                
                // Tenta recuperar algum conteúdo mesmo com erro de JSON
                try
                {
                    // Procura por conteúdo entre aspas que pareça ser a resposta
                    var match = System.Text.RegularExpressions.Regex.Match(responseContent, @"""content"":\s*""([^""]+)""");
                    if (match.Success)
                    {
                        var extracted = match.Groups[1].Value;
                        _logger.LogInformation("Conteúdo recuperado após erro de JSON: {Content}", extracted);
                        return extracted;
                    }
                }
                catch (Exception regexEx)
                {
                    _logger.LogError(regexEx, "Erro na tentativa de recuperação de conteúdo");
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar resposta da OpenAI");
                return string.Empty;
            }
        }

        private async Task<bool> ValidarImagemAsync(string imageUrl)
        {
            try
            {
                _logger.LogInformation("Iniciando validação da imagem: {ImageUrl}", imageUrl);

                // Verifica se a URL é válida
                if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri))
                {
                    _logger.LogWarning("URL de imagem inválida: {ImageUrl}", imageUrl);
                    return false;
                }

                // Verifica se a imagem é do Cloudinary (sua fonte confiável)
                if (!imageUrl.Contains("cloudinary.com"))
                {
                    _logger.LogWarning("Imagem não é do Cloudinary: {ImageUrl}", imageUrl);
                    return false;
                }

                // Opcionalmente, você pode fazer um HEAD request para verificar o content-type
                using var request = new HttpRequestMessage(HttpMethod.Head, imageUrl);
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Falha ao acessar imagem: {ImageUrl}, Status: {Status}", 
                        imageUrl, response.StatusCode);
                    return false;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("image/"))
                {
                    _logger.LogWarning("Tipo de conteúdo inválido: {ContentType}, URL: {ImageUrl}", 
                        contentType, imageUrl);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar imagem: {ImageUrl}", imageUrl);
                return false;
            }
        }

        private string SanitizarPrompt(string prompt)
        {
            // Remove caracteres especiais e palavras-chave potencialmente perigosas
            var sanitized = prompt.Replace("system", "")
                                 .Replace("assistant", "")
                                 .Replace("user", "")
                                 .Replace("role", "")
                                 .Replace("function", "")
                                 .Replace("ignore", "");

            // Remove caracteres de controle
            sanitized = new string(sanitized.Where(c => !char.IsControl(c)).ToArray());

            // Limita o tamanho do prompt
            if (sanitized.Length > 500)
            {
                sanitized = sanitized.Substring(0, 500);
            }

            return sanitized;
        }
    }
}
