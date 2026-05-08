Feature: Household Members

  Scenario: Owner sees all members ordered Owner, Admin, Member
    Given I am logged in with an active household
    And the household also has "alice" as a "admin"
    And the household also has "bob" as a "member"
    When I navigate to "/household/manage"
    Then the household members list shows 3 members
    And the household member "owner" has the role "Owner"
    And the household member "alice" has the role "Admin"
    And the household member "bob" has the role "Member"
    And the household members appear in this order: "owner", "alice", "bob"
