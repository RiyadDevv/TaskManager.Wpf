# TaskManager – WPF .NET Application

## Beschrijving
TaskManager is een desktop WPF-applicatie ontwikkeld in .NET 9.x.  
De applicatie laat gebruikers toe om taken te beheren, te categoriseren en in te plannen in een agenda.  
Gebruikers werken met een persoonlijk account, waarbij rolgebaseerde functionaliteit wordt toegepast (User, PowerUser, Admin).

De applicatie bevat:
- taakbeheer met categorieën
- agenda-planning per datum
- gebruikersbeheer met rollen en blokkering
- KPI-overzicht voor bevoegde gebruikers

---

## Technologieën & Libraries
Dit project maakt gebruik van de volgende technologieën en libraries:

- **.NET 9.x**
- **WPF (Windows Presentation Foundation)** voor de frontend (XAML)
- **Entity Framework Core**
- **ASP.NET Core Identity**
- **SQLite** als lokale databank
- **Microsoft.Extensions.DependencyInjection** voor Dependency Injection

Alle gebruikte libraries zijn Microsoft-standaardbibliotheken en worden gebruikt conform hun respectieve licenties.

---

## Architectuur
De solution bestaat uit twee projecten:

1. **TaskManager.Models** (Class Library)  
   - Bevat alle modellen  
   - Entity Framework DbContext  
   - Identity-integratie  
   - Migraties en seeding  
   - Soft-delete logica  

2. **TaskManager.Wpf** (WPF Desktop Application)  
   - Frontend in XAML  
   - Backend in C#  
   - LoginWindow en MainWindow  
   - Rolgebaseerde UI (tabs)  

---

## Databank & Entity Framework
- De databank wordt lokaal aangemaakt via `update-database`
- Migraties bevinden zich in het Model-project
- De DbContext is afgeleid van `IdentityDbContext`
- Alle modellen maken gebruik van soft-deletes
- Bij eerste opstart worden standaardgegevens (dummy data) aangemaakt om testen mogelijk te maken

---

## Identity & Rollen
- Registratie, login en logout zijn geïmplementeerd
- Er zijn meerdere rollen: **User**, **PowerUser** en **Admin**
- Admin-gebruikers kunnen:
  - rollen wijzigen
  - gebruikers blokkeren/deblokkeren
  - gebruikers soft-deleten
- De UI (tabs) past zich aan op basis van de rol van de gebruiker

---

## Popup Window
De applicatie maakt gebruik van een extra WPF Window naast het hoofdvenster:
- LoginWindow wordt getoond bij het opstarten van de applicatie
- Na succesvolle login wordt het hoofdvenster (MainWindow) geopend en het LoginWindow gesloten

---

## Gebruik van AI-tools
Voor dit project is gebruikgemaakt van AI-tools, met name:
- ChatGPT
- GitHub Copilot

Deze tools werden gebruikt ter ondersteuning bij:
- architectuur en structuur van de applicatie
- codevoorbeelden en suggesties
- verduidelijking van Entity Framework- en Identity-concepten
- optimalisatie en verfijning van bestaande code

Alle code in dit project is volledig begrepen, aangepast en geïntegreerd door de auteur.  

---

## Auteur & Context
- **Naam:** Riyad El Bahri  
- **Vak:** .NET Frameworks  
- **Academiejaar:** 2025–2026  

Dit project werd gerealiseerd als individueel examenproject.