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
        public IAservice(AppDbContext context, IConfiguration configuration, HttpClient httpClient)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClient;
            _apiKey = _configuration["ApiKeys:OPENAI_API_KEY"];
        }

        public async Task<string> GerarDescricaoImagemAsync(List<ClothingItemDTO> looks, string promptUsuario)
        {
           if(looks == null || looks.Count == 0)
           {
                return "Nenhum look fornecido.";
           }

            var messages = new List<object>
            {
                new {
                    role = "system",
                    content = "Você é um especialista em moda. Analise de roupa e recomende combinações de roupas e sugira dicas de estilo. Use as imagens (URLs) para análise, considerando cores, estilos e ocasiões."
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

            var result = JsonSerializer.Deserialize<string>(responseContent);
            return result ?? "Nenhuma descrição gerada.";

        }
    }
}
