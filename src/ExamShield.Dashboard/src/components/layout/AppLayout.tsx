import { useState } from 'react'
import { Link } from 'react-router-dom'
import {
  LayoutDashboard, FileText, Shield, Monitor, ChevronLeft, Bell, User, Menu,
  ClipboardList, ScanLine, Eye, Star, BarChart2, Users, Settings, Search, ShieldCheck,
  BookOpen, KeyRound, FileImage, Trophy, MonitorSmartphone,
} from 'lucide-react'
import { cn } from '../../lib/utils'

const NAV_ITEMS = [
  { label: 'Dashboard',        href: '/',           icon: LayoutDashboard },
  { label: 'Examinations',     href: '/exams',      icon: ClipboardList   },
  { label: 'Capture Sessions', href: '/captures',   icon: Monitor         },
  { label: 'Answer Sheets',   href: '/answer-sheets', icon: FileImage     },
  { label: 'OCR Queue',        href: '/ocr-queue',  icon: ScanLine        },
  { label: 'Manual Review',    href: '/review',     icon: Eye             },
  { label: 'Scoring',          href: '/scoring',    icon: Star            },
  { label: 'Rankings',         href: '/rankings',   icon: Trophy          },
  { label: 'Results',          href: '/results',    icon: BarChart2       },
  { label: 'Audit Logs',       href: '/audit',      icon: FileText        },
  { label: 'Security Center',  href: '/security',   icon: Shield          },
  { label: 'Device Management',href: '/devices',    icon: Monitor         },
  { label: 'Users',            href: '/users',      icon: Users           },
  { label: 'Roles & Perms',   href: '/roles',      icon: ShieldCheck     },
  { label: 'Student Portal',  href: '/student',    icon: Users           },
  { label: 'Reports',         href: '/reports',    icon: BookOpen        },
  { label: 'Public Verify',    href: '/public/verify', icon: Search       },
  { label: 'MFA',              href: '/mfa',         icon: KeyRound        },
  { label: 'Sessions',         href: '/sessions',   icon: MonitorSmartphone },
  { label: 'Settings',         href: '/settings',   icon: Settings        },
]

interface AppLayoutProps {
  userName: string
  children: React.ReactNode
}

export default function AppLayout({ userName, children }: AppLayoutProps) {
  const [collapsed, setCollapsed] = useState(false)

  return (
    <div className="flex h-screen bg-background text-foreground">
      {/* Sidebar */}
      <nav
        aria-label="Sidebar"
        className={cn(
          'flex flex-col border-r border-border bg-card transition-all duration-200 overflow-y-auto',
          collapsed ? 'collapsed w-16' : 'w-56'
        )}
      >
        <div className="flex h-14 items-center justify-between px-4 shrink-0">
          {!collapsed && (
            <span className="text-sm font-bold text-foreground">ExamShield</span>
          )}
          <button
            aria-label="Toggle sidebar"
            onClick={() => setCollapsed(c => !c)}
            className="rounded p-1 hover:bg-muted"
          >
            {collapsed ? <Menu className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
          </button>
        </div>

        <ul className="flex-1 space-y-1 px-2 py-3">
          {NAV_ITEMS.map(item => (
            <li key={item.href}>
              <Link
                to={item.href}
                aria-label={item.label}
                className="flex items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-muted hover:text-foreground"
              >
                <item.icon className="h-4 w-4 shrink-0" />
                {!collapsed && <span>{item.label}</span>}
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      {/* Main area */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* Top nav */}
        <header
          role="banner"
          className="flex h-14 items-center justify-between border-b border-border bg-card px-6 shrink-0"
        >
          <span className="text-sm font-medium text-muted-foreground">Admin Portal</span>
          <div className="flex items-center gap-4">
            <button aria-label="Notifications" className="rounded p-1 hover:bg-muted">
              <Bell className="h-4 w-4" />
            </button>
            <span className="flex items-center gap-2 text-sm text-foreground">
              <User className="h-4 w-4 text-muted-foreground" />
              {userName}
            </span>
          </div>
        </header>

        {/* Content */}
        <main className="flex-1 overflow-auto p-6">{children}</main>
      </div>
    </div>
  )
}
