using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.IAdto
{
    public class LookPromptDTO
    {
        public class ClothingItemDTO
        {
            [Required]
            public string Id { get; set; }

            [Required]
            [StringLength(100)]
            public string Nome { get; set; }

            [Required]
            public string Categoria { get; set; }

            [Required]
            public string Imagem { get; set; }
        }

        public class RecommendationRequestDTO
        {
            [Required]
            public List<ClothingItemDTO> Items { get; set; }

            [Required]
            [StringLength(500)]
            public string Context { get; set; }
        }

        public class RecommendationResponseDTO
        {
            public string OutfitId { get; set; }
            public List<ClothingItemDTO> Items { get; set; }
            public string Context { get; set; }
            public bool Sucesso { get; set; }
            public string Mensagem { get; set; }
        }

        public enum ClothingCategory
        {
            Camisa,
            Calca,
            Short,
            Saia,
            Jaqueta,
            Outro
        }
    }
}
