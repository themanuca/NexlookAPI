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

namespace Application.Services.IAService
{
    public class IAservice:IAIService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IUploadImagemService _uploadImagemService;
        private readonly string promptSystem = "Você é um consultor de moda objetivo e direto. \r\nAnalise as roupas enviadas (imagens) e sugira **1 combinação principal** de look, pronta para ser usada na ocasião informada. \r\n\r\nRegras:\r\n- " +
            "Seja breve e específico (máximo 4 a 5 linhas).\r\n- Liste as peças do look de forma clara.\r\n- Sugira calçado e acessório que completem o estilo.\r\n- Não descreva todas as roupas individualmente, apenas a combinação escolhida.\r\n- Se houver várias opções boas, mostre no máximo 2 variações rápidas. Se possivel, retorne as urls das imagens escolhida\r\n";
        public IAservice(AppDbContext context, IConfiguration configuration, HttpClient httpClient, IUploadImagemService upload)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClient;
            _apiKey = _configuration["ApiKeys:OPENAI_API_KEY"];
            _uploadImagemService = upload;
        }

        public async Task<string> GerarDescricaoImagemAsync(Guid usuarioId, string promptUsuario)
        {
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

            var response = await _httpClient.SendAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI API Error: {responseContent}");
            }

            var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);
            string result = null;

            // Extrair o conteúdo da mensagem da resposta da API
            if (responseObject.TryGetProperty("choices", out JsonElement choices) && 
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out JsonElement message) &&
                message.TryGetProperty("content", out JsonElement content))
            {
                result = content.GetString();
            }

            return result ?? "Nenhuma descrição gerada.";

        }

        public async Task<LookResponse?> GerarDescricaoImagemcomFOTOAsync(Guid usuarioId, string promptUsuario)
        {
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
                throw new Exception($"OpenAI API Error: {responseContent}");
            }

            var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);

            string jsonContent = null;

            if (responseObject.TryGetProperty("choices", out JsonElement choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out JsonElement message) &&
                message.TryGetProperty("content", out JsonElement content))
            {
                jsonContent = content.GetString();
            }
            if (!string.IsNullOrWhiteSpace(jsonContent))
            {
                // Tenta localizar o primeiro '{' e o último '}'
                int start = jsonContent.IndexOf('{');
                int end = jsonContent.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    jsonContent = jsonContent.Substring(start, end - start + 1);
                }
            }
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

    }
}
