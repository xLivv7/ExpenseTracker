using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Models
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Kwota jest wymagana.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Kwota musi być większa od zera.")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }
        [Required(ErrorMessage = "Kategoria nie może być pusta.")]
        [StringLength(50, ErrorMessage = "Kategoria może mieć maksymalnie 50 znaków.")]
        public string Category { get; set; } 
        [StringLength(200, ErrorMessage = "Opis może mieć maksymalnie 200 znaków.")]
        public string Description { get; set; } 
        [Required(ErrorMessage = "Data jest wymagana.")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }
    }
}