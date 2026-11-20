using Microsoft.EntityFrameworkCore;
using NamClient.Web.Models;

namespace NamClient.Web.Data;

public class AppDbContext : DbContext
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 256;
    public const int ClientCodeLength = 6;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<ClientNameWord> ClientNameWords => Set<ClientNameWord>();

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditDates();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditDates();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Client>(client =>
        {
            client.ToTable("Clients");
            client.HasKey(c => c.ClientId);

            client.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(NameMaxLength);

            client.Property(c => c.ClientCode)
                .IsRequired()
                .HasMaxLength(ClientCodeLength)
                .IsFixedLength()
                .IsUnicode(false);

            ConfigureAuditColumns(client);

            client.HasIndex(c => c.ClientCode)
                .IsUnique()
                .HasDatabaseName("UX_Clients_ClientCode");

            client.HasIndex(c => c.Name)
                .IncludeProperties(c => c.ClientCode)
                .HasDatabaseName("IX_Clients_Name");
        });

        modelBuilder.Entity<ClientNameWord>(word =>
        {
            word.ToTable("ClientNameWords");

            word.HasKey(w => new { w.Word, w.ClientId })
                .HasName("PK_ClientNameWords");

            word.Property(w => w.Word)
                .IsRequired()
                .HasMaxLength(ClientNameWord.MaxLength);

            word.HasIndex(w => w.ClientId)
                .HasDatabaseName("IX_ClientNameWords_ClientId");

            word.HasOne(w => w.Client)
                .WithMany(c => c.NameWords)
                .HasForeignKey(w => w.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Contact>(contact =>
        {
            contact.ToTable("Contacts");
            contact.HasKey(c => c.ContactId);
            contact.Ignore(c => c.FullName);

            contact.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(NameMaxLength);

            contact.Property(c => c.Surname)
                .IsRequired()
                .HasMaxLength(NameMaxLength);

            contact.Property(c => c.Email)
                .IsRequired()
                .HasMaxLength(EmailMaxLength);

            ConfigureAuditColumns(contact);

            contact.HasIndex(c => c.Email)
                .IsUnique()
                .HasDatabaseName("UX_Contacts_Email");

            contact.HasIndex(c => new { c.Surname, c.Name })
                .HasDatabaseName("IX_Contacts_Surname_Name");

            contact.HasIndex(c => c.Name)
                .HasDatabaseName("IX_Contacts_Name");
        });

        modelBuilder.Entity<ClientContact>(link =>
        {
            link.ToTable("ClientContacts");

            link.HasKey(cc => new { cc.ClientId, cc.ContactId })
                .HasName("PK_ClientContacts");

            link.HasIndex(cc => cc.ContactId)
                .HasDatabaseName("IX_ClientContacts_ContactId");

            link.HasOne(cc => cc.Client)
                .WithMany(c => c.ClientContacts)
                .HasForeignKey(cc => cc.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            link.HasOne(cc => cc.Contact)
                .WithMany(c => c.ClientContacts)
                .HasForeignKey(cc => cc.ContactId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAuditColumns<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : class, IAuditable
    {
        entity.Property(e => e.CreatedAt)
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.Property(e => e.UpdatedAt)
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }

    private void StampAuditDates()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
