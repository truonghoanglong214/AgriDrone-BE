package com.agridrone.be1;

import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.classes;
import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noClasses;
import static com.tngtech.archunit.library.dependencies.SlicesRuleDefinition.slices;

import com.tngtech.archunit.core.domain.JavaClasses;
import com.tngtech.archunit.core.importer.ClassFileImporter;
import com.tngtech.archunit.core.importer.ImportOption;
import org.junit.jupiter.api.Test;
import org.springframework.web.bind.annotation.RestController;

class ArchitectureRulesTest {

    private static final JavaClasses CLASSES = new ClassFileImporter()
            .withImportOption(ImportOption.Predefined.DO_NOT_INCLUDE_TESTS)
            .importPackages("com.agridrone.be1");

    @Test
    void domainStaysIndependentFromFrameworkAndAdapters() {
        noClasses()
                .that().resideInAPackage("..domain..")
                .should().dependOnClassesThat().resideInAnyPackage(
                        "org.springframework..",
                        "jakarta.persistence..",
                        "jakarta.servlet..",
                        "..api..",
                        "..infrastructure..")
                .check(CLASSES);
    }

    @Test
    void apiAndApplicationDoNotReachIntoInfrastructure() {
        noClasses()
                .that().resideInAnyPackage("..api..", "..application..", "..domain..")
                .should().dependOnClassesThat().resideInAPackage("..infrastructure..")
                .check(CLASSES);
    }

    @Test
    void identityPersistenceUsesJpaInsteadOfJdbcTemplate() {
        noClasses()
                .that().resideInAPackage("..identity..")
                .should().dependOnClassesThat().haveFullyQualifiedName(
                        "org.springframework.jdbc.core.JdbcTemplate")
                .check(CLASSES);
    }

    @Test
    void identityOutputPortsAndPersistenceAdaptersFollowNaming() {
        classes()
                .that().resideInAPackage("..identity.application.port.out..")
                .should().beInterfaces()
                .check(CLASSES);

        classes()
                .that().resideInAPackage(
                        "..identity.application.port.out.persistence..")
                .should().haveSimpleNameEndingWith("Repository")
                .check(CLASSES);

        classes()
                .that().resideInAPackage(
                        "..identity.infrastructure.persistence.jpa.repository..")
                .should().beInterfaces()
                .andShould().haveSimpleNameEndingWith("JpaRepository")
                .check(CLASSES);

        classes()
                .that().resideInAPackage(
                        "..identity.infrastructure.persistence.jpa.adapter..")
                .should().haveSimpleNameEndingWith("PersistenceAdapter")
                .check(CLASSES);
    }

    @Test
    void sharedFoundationDoesNotDependOnBusinessModules() {
        noClasses()
                .that().resideInAPackage("..shared..")
                .should().dependOnClassesThat().resideInAnyPackage(
                        "..identity..",
                        "..farm..",
                        "..plant..",
                        "..survey..")
                .check(CLASSES);
    }

    @Test
    void businessModulesDoNotUseAnotherModulesAdapters() {
        assertNoForeignAdapterDependency("identity", "farm", "plant", "survey");
        assertNoForeignAdapterDependency("farm", "identity", "plant", "survey");
        assertNoForeignAdapterDependency("plant", "identity", "farm", "survey");
        assertNoForeignAdapterDependency("survey", "identity", "farm", "plant");
    }

    @Test
    void modulesAreFreeOfCycles() {
        slices()
                .matching("com.agridrone.be1.(*)..")
                .should().beFreeOfCycles()
                .check(CLASSES);
    }

    @Test
    void restControllersLiveOnlyInApiPackages() {
        classes()
                .that().areAnnotatedWith(RestController.class)
                .should().resideInAPackage("..api..")
                .allowEmptyShould(true)
                .check(CLASSES);
    }

    private static void assertNoForeignAdapterDependency(
            String source,
            String firstTarget,
            String secondTarget,
            String thirdTarget) {
        noClasses()
                .that().resideInAPackage("com.agridrone.be1." + source + "..")
                .should().dependOnClassesThat().resideInAnyPackage(
                        "com.agridrone.be1." + firstTarget + ".api..",
                        "com.agridrone.be1." + firstTarget + ".infrastructure..",
                        "com.agridrone.be1." + secondTarget + ".api..",
                        "com.agridrone.be1." + secondTarget + ".infrastructure..",
                        "com.agridrone.be1." + thirdTarget + ".api..",
                        "com.agridrone.be1." + thirdTarget + ".infrastructure..")
                .check(CLASSES);
    }
}
