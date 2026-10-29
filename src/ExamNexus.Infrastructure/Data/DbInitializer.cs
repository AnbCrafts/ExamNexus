using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Domain.Entities;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedDatabaseAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Tenants
        var tenantAcmeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var tenantApexId = Guid.Parse("22222222-3333-4444-5555-666666666666");

        if (!await context.Tenants.AnyAsync())
        {
            var tenants = new List<Tenant>
            {
                new()
                {
                    Id = tenantAcmeId,
                    Identifier = "acme-corp",
                    Name = "Acme Global Corporate Academy",
                    Plan = TenantPlan.Enterprise,
                    MaxCandidateSlots = 2000,
                    BrandColorHex = "#0284c7",
                    IsActive = true
                },
                new()
                {
                    Id = tenantApexId,
                    Identifier = "apex-university",
                    Name = "Apex Institute of Technology",
                    Plan = TenantPlan.Professional,
                    MaxCandidateSlots = 1000,
                    BrandColorHex = "#7c3aed",
                    IsActive = true
                }
            };

            await context.Tenants.AddRangeAsync(tenants);
            await context.SaveChangesAsync();
        }

        // 2. Seed Roles
        string[] roles = { "SystemAdmin", "TenantAdmin", "Instructor", "Proctor", "Candidate" };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName, $"ExamNexus {roleName} Role"));
            }
        }

        // 3. Seed Users for Acme Corp
        var adminEmail = "admin@acme.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Marcus",
                LastName = "Vance",
                UserCode = "ADM-001",
                TenantId = tenantAcmeId,
                IsActive = true
            };
            if ((await userManager.CreateAsync(adminUser, "Admin@123!")).Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "TenantAdmin");
            }
        }

        var instructorEmail = "instructor@acme.com";
        var instructorUser = await userManager.FindByEmailAsync(instructorEmail);
        if (instructorUser == null)
        {
            instructorUser = new ApplicationUser
            {
                UserName = instructorEmail,
                Email = instructorEmail,
                EmailConfirmed = true,
                FirstName = "Dr. Aris",
                LastName = "Thorne",
                UserCode = "INS-101",
                TenantId = tenantAcmeId,
                IsActive = true
            };
            if ((await userManager.CreateAsync(instructorUser, "Instructor@123!")).Succeeded)
            {
                await userManager.AddToRoleAsync(instructorUser, "Instructor");
            }
        }

        var proctorEmail = "proctor@acme.com";
        var proctorUser = await userManager.FindByEmailAsync(proctorEmail);
        if (proctorUser == null)
        {
            proctorUser = new ApplicationUser
            {
                UserName = proctorEmail,
                Email = proctorEmail,
                EmailConfirmed = true,
                FirstName = "Sarah",
                LastName = "Lin",
                UserCode = "PRO-201",
                TenantId = tenantAcmeId,
                IsActive = true
            };
            if ((await userManager.CreateAsync(proctorUser, "Proctor@123!")).Succeeded)
            {
                await userManager.AddToRoleAsync(proctorUser, "Proctor");
            }
        }

        var studentEmail = "student@acme.com";
        var studentUser = await userManager.FindByEmailAsync(studentEmail);
        if (studentUser == null)
        {
            studentUser = new ApplicationUser
            {
                UserName = studentEmail,
                Email = studentEmail,
                EmailConfirmed = true,
                FirstName = "Alex",
                LastName = "Rivera",
                UserCode = "STD-901",
                TenantId = tenantAcmeId,
                IsActive = true
            };
            if ((await userManager.CreateAsync(studentUser, "Student@123!")).Succeeded)
            {
                await userManager.AddToRoleAsync(studentUser, "Candidate");
            }
        }

        // 4. Seed Course, Question Bank, Questions, and Options (for Acme Corp)
        var courseId = Guid.Parse("A1111111-1111-1111-1111-111111111111");
        if (!await context.Courses.IgnoreQueryFilters().AnyAsync(c => c.Id == courseId) && instructorUser != null)
        {
            var course = new Course
            {
                Id = courseId,
                TenantId = tenantAcmeId,
                Code = "AZ-900",
                Title = "Microsoft Azure Fundamentals",
                Description = "Core architectural concepts, cloud models, security compliance, and SLAs.",
                CreatedByInstructorId = instructorUser.Id
            };

            var bankId = Guid.NewGuid();
            var bank = new QuestionBank
            {
                Id = bankId,
                TenantId = tenantAcmeId,
                CourseId = courseId,
                Title = "Cloud Concepts & Security Fundamentals",
                Description = "IaaS, PaaS, SaaS, and Zero-Trust identity questions."
            };

            // Question 1
            var q1Id = Guid.NewGuid();
            var q1 = new Question
            {
                Id = q1Id,
                TenantId = tenantAcmeId,
                BankId = bankId,
                Text = "Which cloud service model provides the customer with the highest degree of infrastructure control?",
                Points = 10.00m,
                QuestionType = QuestionType.MultipleChoice,
                Explanation = "Infrastructure-as-a-Service (IaaS) gives users control over the OS, storage, and networking."
            };
            q1.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q1Id, OptionText = "Software as a Service (SaaS)", IsCorrect = false, OrderIndex = 1 });
            q1.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q1Id, OptionText = "Platform as a Service (PaaS)", IsCorrect = false, OrderIndex = 2 });
            q1.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q1Id, OptionText = "Infrastructure as a Service (IaaS)", IsCorrect = true, OrderIndex = 3 });
            q1.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q1Id, OptionText = "Function as a Service (Serverless)", IsCorrect = false, OrderIndex = 4 });

            // Question 2
            var q2Id = Guid.NewGuid();
            var q2 = new Question
            {
                Id = q2Id,
                TenantId = tenantAcmeId,
                BankId = bankId,
                Text = "What is the primary benefit of deploying resources across multiple Azure Availability Zones?",
                Points = 10.00m,
                QuestionType = QuestionType.MultipleChoice,
                Explanation = "Availability Zones protect applications and data from datacenter-level failures."
            };
            q2.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q2Id, OptionText = "Automatic reduction in compute pricing", IsCorrect = false, OrderIndex = 1 });
            q2.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q2Id, OptionText = "High availability & protection against datacenter failures", IsCorrect = true, OrderIndex = 2 });
            q2.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q2Id, OptionText = "Elimination of identity access policies", IsCorrect = false, OrderIndex = 3 });
            q2.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q2Id, OptionText = "Unlimited storage capacity on local machines", IsCorrect = false, OrderIndex = 4 });

            // Question 3
            var q3Id = Guid.NewGuid();
            var q3 = new Question
            {
                Id = q3Id,
                TenantId = tenantAcmeId,
                BankId = bankId,
                Text = "Which security principle asserts that every access request must be explicitly verified and authenticated, assuming breach?",
                Points = 10.00m,
                QuestionType = QuestionType.MultipleChoice,
                Explanation = "The Zero Trust security model operates on the principle of 'never trust, always verify'."
            };
            q3.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q3Id, OptionText = "Perimeter Security (Firewall Only)", IsCorrect = false, OrderIndex = 1 });
            q3.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q3Id, OptionText = "Zero Trust Architecture", IsCorrect = true, OrderIndex = 2 });
            q3.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q3Id, OptionText = "Implicit Corporate Trust", IsCorrect = false, OrderIndex = 3 });
            q3.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q3Id, OptionText = "Static IP Whitelisting", IsCorrect = false, OrderIndex = 4 });

            // Question 4
            var q4Id = Guid.NewGuid();
            var q4 = new Question
            {
                Id = q4Id,
                TenantId = tenantAcmeId,
                BankId = bankId,
                Text = "Which cloud computing attribute refers to the capability of automatically scaling computational resources up or out based on demand?",
                Points = 10.00m,
                QuestionType = QuestionType.MultipleChoice,
                Explanation = "Elasticity allows cloud systems to dynamically allocate resources in response to traffic fluctuations."
            };
            q4.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q4Id, OptionText = "Fault Tolerance", IsCorrect = false, OrderIndex = 1 });
            q4.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q4Id, OptionText = "Elasticity / Auto-scaling", IsCorrect = true, OrderIndex = 2 });
            q4.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q4Id, OptionText = "Data Redundancy", IsCorrect = false, OrderIndex = 3 });
            q4.Options.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionId = q4Id, OptionText = "Latency Buffering", IsCorrect = false, OrderIndex = 4 });

            await context.Courses.AddAsync(course);
            await context.QuestionBanks.AddAsync(bank);
            await context.Questions.AddRangeAsync(q1, q2, q3, q4);
            await context.SaveChangesAsync();

            // 5. Seed Exam linked to the 4 questions
            var examId = Guid.Parse("E1111111-1111-1111-1111-111111111111");
            var exam = new Exam
            {
                Id = examId,
                TenantId = tenantAcmeId,
                CourseId = courseId,
                Title = "AZ-900 Midterm Proctored Certification Assessment",
                AccessCode = "AZ900-LIVE",
                DurationMinutes = 20,
                PassingScorePercent = 75.00m,
                TotalMarks = 40.00m,
                MaxTabSwitchesAllowed = 2,
                IsActive = true,
                CreatedById = instructorUser.Id
            };

            exam.ExamQuestions.Add(new ExamQuestion { Id = Guid.NewGuid(), TenantId = tenantAcmeId, ExamId = examId, QuestionId = q1Id, OrderIndex = 1 });
            exam.ExamQuestions.Add(new ExamQuestion { Id = Guid.NewGuid(), TenantId = tenantAcmeId, ExamId = examId, QuestionId = q2Id, OrderIndex = 2 });
            exam.ExamQuestions.Add(new ExamQuestion { Id = Guid.NewGuid(), TenantId = tenantAcmeId, ExamId = examId, QuestionId = q3Id, OrderIndex = 3 });
            exam.ExamQuestions.Add(new ExamQuestion { Id = Guid.NewGuid(), TenantId = tenantAcmeId, ExamId = examId, QuestionId = q4Id, OrderIndex = 4 });

            await context.Exams.AddAsync(exam);
            await context.SaveChangesAsync();
        }
    }
}
