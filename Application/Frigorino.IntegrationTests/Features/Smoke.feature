Feature: Smoke

  Scenario: App home page renders
    Given I am logged in as "owner"
    When I navigate to "/"
    Then the page title should contain "Frigorino"
