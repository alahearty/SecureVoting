# Final-Year Project Report Outline

## Chapter One - Introduction

1.1 Background to departmental student elections  
1.2 Statement of the problem (manual voting, eligibility disputes, duplicate voting, weak result records)  
1.3 Aim and objectives  
1.4 Significance of the study  
1.5 Scope and delimitation  
1.6 Assumptions and limitations  
1.7 Definition of terms (ballot, entitlement, turnout, audit chain, RBAC)

## Chapter Two - Literature Review

2.1 Electronic voting concepts and classifications  
2.2 Authentication and identity verification in student elections  
2.3 Ballot secrecy and unlinkability  
2.4 One-vote enforcement and concurrency control  
2.5 Result verification, publication and auditability  
2.6 Web security threats relevant to elections  
2.7 Review of related systems and gap analysis  
2.8 Summary

## Chapter Three - System Analysis and Design

3.1 Research/development methodology  
3.2 Requirements elicitation and analysis  
3.3 Functional requirements  
3.4 Non-functional requirements  
3.5 Use-case model  
3.6 System architecture  
3.7 Database design and ERD  
3.8 Ballot privacy model and one-vote transaction design  
3.9 Threat model and security controls  
3.10 UI/UX design principles  
3.11 Deployment architecture

## Chapter Four - Implementation and Testing

4.1 Development environment and technology stack  
4.2 Backend implementation layers  
4.3 Authentication, authorization and account provisioning  
4.4 Election and candidate management  
4.5 Ballot submission and atomic one-vote enforcement  
4.6 Result calculation, verification and publication  
4.7 Audit hash chain  
4.8 Vue frontend and role portals  
4.9 Docker/CI implementation  
4.10 Testing strategy and test cases  
4.11 Actual test results and discussion  
4.12 Security findings and residual risks

## Chapter Five - Summary, Conclusion and Recommendations

5.1 Summary of work  
5.2 Achievement of objectives  
5.3 Limitations  
5.4 Recommendations for further work  
5.5 Conclusion

## Appendices

- Requirements specification
- API/route catalogue and OpenAPI reference
- Database schema/ERD
- Test results and commands
- Student and administrator guides
- Demonstration script
- Ethical/data-protection statement
- Source-code structure

## Suggested evaluation questions

- Does the system prove one-vote enforcement under concurrency?
- Are ballot selections inaccessible through ordinary identity/report APIs?
- Are election lifecycle transitions and result versions controlled and auditable?
- What happens when the database or server is unavailable?
- Which security claims remain outside the current evidence?
