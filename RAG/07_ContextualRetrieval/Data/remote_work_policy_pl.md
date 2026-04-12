# Remote Work Policy – Warsaw Office (PL) – Effective 2026

**Document Reference:** PL-HR-OPS-2026-04  
**Classification:** Internal / Confidential  
**Owner:** Warsaw Human Resources Department  
**Applicability:** Full-time employees based in Poland  
**Version:** 4.2.5 (Stable)

-----

## 1\. Executive Summary

This document defines the hybrid and remote work model for the Warsaw operational hub. As we scale our intelligent application suites within the Microsoft Azure ecosystem, providing a flexible yet secure environment for our engineers and architects is paramount. This policy balances individual flexibility with the rigorous security requirements of our global clients. It is the definitive guide for all staff registered under the Polish legal entity.

## 2\. Introduction and Legal Framework

This policy applies strictly to full-time staff based in Poland. It is designed to comply with the Polish Labor Code (Kodeks Pracy), specifically the 2023 amendments regarding remote work (praca zdalna). All employees covered by this policy are registered at the Warsaw headquarters, regardless of their daily physical presence in the office. This framework ensures that the company remains compliant with local labor inspections (PIP) while fostering a modern, distributed engineering culture.

## 3\. Definitions

  * **Warsaw Hub:** The primary physical office located in the Warsaw central business district.
  * **Occasional Remote Work:** Remote work performed at the request of the employee for a maximum of 24 days per calendar year.
  * **Permanent Hybrid:** A model requiring 40% physical presence in the Warsaw Hub per month.
  * **The Company:** Refers specifically to the Polish subsidiary of the global organization.

## 4\. Eligibility and Performance Standards

Eligibility for remote work is determined by the functional requirements of the role. For the "Deployed in Azure" engineering group, roles such as .NET Backend Engineer, AI Solutions Architect, and DevOps Lead are eligible by default, subject to manager approval and successful completion of the onboarding sequence.

### 4.1 Performance Monitoring

Performance in a remote setting is measured via Azure DevOps (ADO) velocity, Jira ticket completion rates, and active participation in asynchronous architectural reviews. Employees must maintain a "Meeting Expectations" rating or higher to remain eligible for the 100% remote work option.

-----

## 5\. Financial Provisions and Equipment

The company provides the physical and financial infrastructure to ensure a "production-grade" environment at home.

### 5.1 Corporate Assets

All computing hardware (Laptops, 4K Monitors, Peripherals) is provided by the Warsaw IT Procurement team. These assets are managed via Microsoft Intune and remain the property of the company. Standard issue for engineers includes a high-spec laptop capable of running heavy containerized workloads and local LLM instances for testing.

### 5.2 Home Office Equipment Allowance

**Employees are eligible for a $500 annual reimbursement to cover home office equipment, provided they have completed their 3-month probation period.** This allowance is specifically intended for the purchase of ergonomic desks, professional-grade seating, or high-lumen desk lighting. This reimbursement is a one-time annual payment. To claim this benefit, employees must submit a "Benefit Request" via the Internal Portal and attach a valid VAT invoice addressed to the Warsaw legal entity. Invoices lacking the company’s NIP (Tax Identification Number) will be automatically rejected by the automated payroll system.

### 5.3 Internet and Utility Subsidy

Pursuant to Article 67(24) of the Labor Code, the company pays a monthly lump sum (ekwiwalent) to cover the costs of electricity and telecommunication services used during working hours. This amount is adjusted annually based on the Polish Consumer Price Index (CPI) and current energy tariffs in the Masovian Voivodeship.

-----

## 6\. Data Security and Cloud Access

Given our deep integration with the Microsoft Azure ecosystem, security is non-negotiable.

### 6.1 Conditional Access and Entra ID

Access to the Azure Management Portal, Azure AI Foundry, and DevOps pipelines is governed by Microsoft Entra ID (formerly Azure AD). Remote access is only permitted from managed devices that meet the "Compliant" status in Intune. This includes active disk encryption (BitLocker), up-to-date antivirus definitions, and the absence of unauthorized administrative tools.

### 6.2 Geographic Restrictions (Geo-fencing)

While work is remote, it must remain within the borders of Poland unless a "Work from Abroad" (WFA) permit is granted by HR for a specified period (maximum 14 days). Access to production environments is strictly blocked for IP ranges outside of the European Economic Area (EEA) to ensure GDPR compliance and adherence to client-specific data sovereignty agreements.

-----

## 7\. Occupational Health and Safety (BHP)

In accordance with Polish BHP regulations, every remote employee must perform a self-assessment of their workstation. The "Remote Work Station Declaration" must be signed digitally and uploaded to the HR portal before remote work commences.

### 7.1 Ergonomic Standards

The company recommends a minimum desk depth of 80cm and a chair with 4D armrest adjustment. The screen must be positioned so that the top edge is at eye level. Proper lighting (minimum 500 lux) is required to prevent eye strain during long coding sessions or architectural planning.

[... Page 15-20: Detailed descriptions of lumbar support angles, monitor height relative to eye level, fire safety protocols for apartment buildings, and mandatory 5-minute "Screen Breaks" for every hour of continuous work ...]

-----

## 8\. Working Hours and Communication Protocols

Core hours for the Warsaw hub are 09:00 to 17:00 CET. During these hours, employees must be available on Microsoft Teams.

### 8.1 Meeting Etiquette

For distributed teams, cameras must be turned on during "Architecture Design Sessions" and "Sprint Plannings" to foster better collaboration. "Deep Work" blocks (No-Meeting zones) are encouraged between 13:00 and 15:00 CET daily.

-----

## 9\. Tax Implications and Local Regulations

Remote work does not change the tax residency of the employee. All social security contributions (ZUS) and personal income tax (PIT) will continue to be withheld by the Polish payroll office.

-----

## 10\. Data Residency and Sovereign Cloud Requirements

Engineers working on "Project Aurora" or "Sovereign AI" must ensure that no customer data is downloaded to local drives. All development must occur within Azure DevBox or GitHub Codespaces instances located in the `polandcentral` region.

-----

## 11\. Termination of Remote Work Agreement

The company reserves the right to terminate the remote work arrangement with a 14-day notice period if there is a documented drop in performance or a breach of the "Data Security and Cloud Access" protocol. Upon termination of the agreement, the employee is required to return all corporate assets to the Warsaw Hub within 3 business days.

-----

## 12\. Appendices

### Appendix A: Approved Vendor List (Warsaw Local)

  * Office-Pro Poland (Warsaw, ul. Marszałkowska)
  * Warsaw Ergo-Center (Warsaw, ul. Chmielna)
  * IT-Supplies PL (Online/Warsaw Distribution Center)

### Appendix B: VAT Invoice Requirements

All invoices must include the company's NIP and the specific Warsaw office address to be eligible for the £500 reimbursement mentioned in Section 5.2. Note that the reimbursement is processed in PLN at the mid-market exchange rate provided by the National Bank of Poland (NBP) on the day of the invoice.

### Appendix C: Intune Compliance Checklist

1.  TPM 2.0 Enabled
2.  Secure Boot Active
3.  BitLocker Encryption (XTS-AES 256)
4.  Windows Defender Real-time Protection On
5.  Microsoft Authenticator (MFA) Configured

-----

*End of Document*