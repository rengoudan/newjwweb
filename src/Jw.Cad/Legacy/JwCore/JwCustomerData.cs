using System.ComponentModel.DataAnnotations;
namespace JwCore
{
    public class JwCustomerData:BaseEntityData
    {
        [Required]
        public virtual string CompanyName { get; set; }

        public virtual string CompanyAddress { get; set; } = "";

        public virtual string Contact { get; set; } = "";

        public virtual string Telephone { get; set; } = "";

        public virtual List<JwProjectMainData> JwProjectMainDatas { get; } = new();
    }
}
