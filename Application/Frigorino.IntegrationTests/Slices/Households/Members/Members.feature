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

  Scenario: Owner adds an existing user as a member
    Given I am logged in with an active household
    And a user "alice" exists
    When I navigate to "/household/manage"
    And I open the add member dialog
    And I fill in the new member email "alice@test.frigorino.local"
    And I submit the add member form
    Then the household members list shows 2 members
    And the household member "alice" has the role "Member"

  Scenario: Adding an unknown email shows an inline error
    Given I am logged in with an active household
    When I navigate to "/household/manage"
    And I open the add member dialog
    And I fill in the new member email "nobody@nowhere.test"
    And I submit the add member form
    Then the add member error contains "No user with that email"
    And the household members list shows 1 members

  Scenario: Adding an existing active member shows an inline error
    Given I am logged in with an active household
    And the household also has "alice" as a "member"
    When I navigate to "/household/manage"
    And I open the add member dialog
    And I fill in the new member email "alice@test.frigorino.local"
    And I submit the add member form
    Then the add member error contains "already a member"
    And the household members list shows 2 members
