using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock;
using System;
using System.Collections.Generic;

namespace PKISharp.WACS.UnitTests.Tests.RenewalTests
{
    [TestClass]
    public class RenewalExecutorTests
    {
        public static void ShouldRun(Renewal renewal, bool outcome)
        {
            renewal.LastFriendlyName = "UnitTest";
            var container = MockContainer.TestScope();
            var dueDate = container.Resolve<DueDateStaticService>();
            Assert.AreEqual(outcome, dueDate.IsDue(renewal));
        }

        [TestMethod]
        public void NewRenewal()
        {
            ShouldRun(new Renewal() { New = true }, true);
        }

        [TestMethod]
        public void UpdateRenewal()
        {
            ShouldRun(new Renewal() { Updated = true }, true);
        }
        [TestMethod]
        public void NotDueRenewal()
        {
            ShouldRun(
                new Renewal() { 
                    History = new List<RenewResult>() {
                        new() {
                            Date = DateTime.Now.AddDays(-3),
                            Success = true
                        }
                    } 
                },
                false);
        }

        [TestMethod]
        public void DueRenewal()
        {
            ShouldRun(
                new Renewal()
                {
                    History = new List<RenewResult>() {
                        new() {
                            Date = DateTime.Now.AddDays(-100),
                            Success = true
                        }
                    }
                },
                true);
        }

        [TestMethod]
        public void FailingRenewal()
        {
            ShouldRun(
                new Renewal()
                {
                    History = new List<RenewResult>() {
                        new() {
                            Date = DateTime.Now.AddDays(-3),
                            Success = false
                        }
                    }
                },
                true);
        }

        [TestMethod]
        public void LegacyRenewal()
        {
            ShouldRun(
                new Renewal()
                {
                    History = new List<RenewResult>() {
                        new() {
                            Date = DateTime.Now.AddDays(-3),
                            Success = true,
                            ThumbprintsJson = new List<string> {"bla"}
                        }
                    }
                },
                false);
        }

        [TestMethod]
        public void MissingOrder()
        {
            ShouldRun(
                new Renewal()
                {
                    History = new List<RenewResult>() {
                        new() {
                            Date = new DateTime(2022,1,1),
                            Success = true,
                            OrderResults = new List<OrderResult>()
                            {
                                new("strange")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2023,1,1)
                                },
                                new("normal")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2023,1,1)
                                }
                            }
                        },
                        new() {
                            Date = DateTime.Now.AddDays(-3),
                            Success = true,
                            OrderResults = new List<OrderResult>()
                            {
                                new("strange")
                                {
                                    Missing = true
                                },
                                new("normal")
                                {
                                    Success = true,
                                    ExpireDate = DateTime.Now.AddDays(20)
                                }
                            }
                        }
                    }
                },
                false);
        }

        [TestMethod]
        public void UnmissingOrder()
        {
            ShouldRun(
                new Renewal()
                {
                    History = new List<RenewResult>() {
                        new() {
                            Date = new DateTime(2022,1,1),
                            Success = true,
                            OrderResults = new List<OrderResult>()
                            {
                                new("strange")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2023,1,1)
                                },
                                new("normal")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2023,1,1)
                                }
                            }
                        },
                        new() {
                            Date = DateTime.Now.AddDays(-3),
                            Success = true,
                            OrderResults = new List<OrderResult>()
                            {
                                new("normal")
                                {
                                    Success = true,
                                    ExpireDate = DateTime.Now.AddDays(20)
                                }
                            }
                        }
                    }
                },
                true);
        }

        [TestMethod]
        public void ReturnedOrder()
        {
            ShouldRun(
                new Renewal()
                {
                    History = new List<RenewResult>() {
                        new() {
                            Date = new DateTime(2021,1,1),
                            Success = true,
                            OrderResults = new List<OrderResult>()
                            {
                                new("strange")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2022,1,1)
                                },
                                new("normal")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2022,1,1)
                                }
                            }
                        },
                        new() {
                            Date = new DateTime(2022,2,1),
                            Success = true,
                            OrderResults = new List<OrderResult>()
                            {
                                new("strange")
                                {
                                    Missing = true
                                },
                                new("normal")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2022,2,1)
                                }
                            }
                        },
                        new() {
                            Date = new DateTime(2023,2,1),
                            Success = true,
                            OrderResults = new List<OrderResult>()
                            {
                                new("strange")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2023,2,28)
                                },
                                new("normal")
                                {
                                    Success = true,
                                    ExpireDate = new DateTime(2023,2,28)
                                }
                            }
                        }
                    }
                },
                true);
        }

    }
}
